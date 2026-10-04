using System.Runtime.InteropServices;

namespace ChurchTimeTracker.Services;

public sealed class GlobalShortcutService
{
    private const int ShortcutIdBase = 0x5100;
    private readonly TimerService timer;

#if WINDOWS
    public const uint WindowsHotKeyMessage = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private nint windowHandle;
#elif MACCATALYST
    private const string CarbonFramework = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const uint EventClassKeyboard = 0x6B657962;
    private const uint EventHotKeyPressed = 6;
    private const uint EventParamDirectObject = 0x2D2D2D2D;
    private const uint TypeEventHotKeyId = 0x686B6964;
    private const uint ControlKey = 1u << 12;
    private const uint OptionKey = 1u << 11;
    private readonly List<nint> hotKeyReferences = [];
    private EventHandlerDelegate? eventHandlerDelegate;
    private nint eventHandlerReference;
#endif

    public string? Warning { get; private set; }
    public event Action? Changed;

    public GlobalShortcutService(TimerService timerService)
    {
        timer = timerService;
    }

    public static string LabelForSlot(int slotNumber) =>
        $"Ctrl+Alt+{(slotNumber == 10 ? 0 : slotNumber)}";

    public void Register(Window window)
    {
        Unregister();
        List<int> failedSlots = [];

#if WINDOWS
        if (window.Handler?.PlatformView is not Microsoft.Maui.MauiWinUIWindow nativeWindow)
        {
            Warning = "Keyboard shortcuts could not attach to the app window.";
            Changed?.Invoke();
            return;
        }

        windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        for (int slotNumber = 1; slotNumber <= 10; slotNumber++)
        {
            uint virtualKey = slotNumber == 10 ? 0x30u : 0x30u + (uint)slotNumber;
            if (!RegisterHotKey(windowHandle, ShortcutIdBase + slotNumber,
                    ModControl | ModAlt | ModNoRepeat, virtualKey))
            {
                failedSlots.Add(slotNumber);
            }
        }
#elif MACCATALYST
        eventHandlerDelegate = HandleMacEvent;
        EventTypeSpec eventType = new()
        {
            EventClass = EventClassKeyboard,
            EventKind = EventHotKeyPressed
        };

        int handlerStatus = InstallApplicationEventHandler(
            eventHandlerDelegate,
            1,
            [eventType],
            nint.Zero,
            out eventHandlerReference);

        if (handlerStatus != 0)
        {
            Warning = "Global keyboard shortcuts are unavailable on this Mac.";
            Changed?.Invoke();
            return;
        }

        uint[] keyCodes = [0x12, 0x13, 0x14, 0x15, 0x17, 0x16, 0x1A, 0x1C, 0x19, 0x1D];
        for (int index = 0; index < keyCodes.Length; index++)
        {
            int slotNumber = index + 1;
            EventHotKeyId id = new() { Signature = 0x43545431, Id = (uint)slotNumber };
            int status = RegisterEventHotKey(
                keyCodes[index],
                ControlKey | OptionKey,
                id,
                GetApplicationEventTarget(),
                0,
                out nint hotKeyReference);

            if (status == 0)
            {
                hotKeyReferences.Add(hotKeyReference);
            }
            else
            {
                failedSlots.Add(slotNumber);
            }
        }
#endif

        Warning = failedSlots.Count == 0
            ? null
            : $"Shortcuts unavailable for slot{(failedSlots.Count == 1 ? string.Empty : "s")} {string.Join(", ", failedSlots)} because another app is using them.";
        Changed?.Invoke();
    }

    public void Unregister()
    {
#if WINDOWS
        if (windowHandle != nint.Zero)
        {
            for (int slotNumber = 1; slotNumber <= 10; slotNumber++)
            {
                UnregisterHotKey(windowHandle, ShortcutIdBase + slotNumber);
            }
            windowHandle = nint.Zero;
        }
#elif MACCATALYST
        foreach (nint reference in hotKeyReferences)
        {
            UnregisterEventHotKey(reference);
        }
        hotKeyReferences.Clear();

        if (eventHandlerReference != nint.Zero)
        {
            RemoveEventHandler(eventHandlerReference);
            eventHandlerReference = nint.Zero;
        }
        eventHandlerDelegate = null;
#endif
    }

#if WINDOWS
    public void HandleWindowsMessage(nint parameter)
    {
        int slotNumber = (int)parameter - ShortcutIdBase;
        if (slotNumber is >= 1 and <= 10)
        {
            RunSlot(slotNumber);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
#elif MACCATALYST
    private int HandleMacEvent(nint nextHandler, nint eventReference, nint userData)
    {
        int status = GetEventParameter(
            eventReference,
            EventParamDirectObject,
            TypeEventHotKeyId,
            out _,
            (uint)Marshal.SizeOf<EventHotKeyId>(),
            out _,
            out EventHotKeyId id);

        if (status == 0 && id.Id is >= 1 and <= 10)
        {
            RunSlot((int)id.Id);
        }

        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyId
    {
        public uint Signature;
        public uint Id;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EventHandlerDelegate(nint nextHandler, nint eventReference, nint userData);

    [DllImport(CarbonFramework)]
    private static extern nint GetApplicationEventTarget();

    [DllImport(CarbonFramework)]
    private static extern int InstallApplicationEventHandler(
        EventHandlerDelegate handler,
        uint eventTypeCount,
        [In] EventTypeSpec[] eventTypes,
        nint userData,
        out nint eventHandlerReference);

    [DllImport(CarbonFramework)]
    private static extern int RemoveEventHandler(nint eventHandlerReference);

    [DllImport(CarbonFramework)]
    private static extern int RegisterEventHotKey(
        uint keyCode,
        uint modifiers,
        EventHotKeyId hotKeyId,
        nint target,
        uint options,
        out nint hotKeyReference);

    [DllImport(CarbonFramework)]
    private static extern int UnregisterEventHotKey(nint hotKeyReference);

    [DllImport(CarbonFramework)]
    private static extern int GetEventParameter(
        nint eventReference,
        uint parameterName,
        uint desiredType,
        out uint actualType,
        uint bufferSize,
        out uint actualSize,
        out EventHotKeyId data);
#endif

    private void RunSlot(int slotNumber)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await timer.SwitchToSlot(slotNumber);
            }
            catch (Exception exception)
            {
                Warning = $"Shortcut failed: {exception.Message}";
                Changed?.Invoke();
            }
        });
    }
}
