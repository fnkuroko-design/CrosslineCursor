using CrosshairOverlayTool;

var failures = 0;
var count = 0;
void Test(string name, Action test)
{
    count++;
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}

void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

const uint Scan = 0x3A;
const uint Eisu = NativeMethods.VK_DBE_ALPHANUMERIC;
NativeMethods.KBDLLHOOKSTRUCT Key(uint vk = Eisu, uint scan = Scan, uint flags = 0)
    => new() { vkCode = vk, scanCode = scan, flags = flags };
RawKeyboardEventArgs Raw(bool up = false, ushort vk = (ushort)Eisu, uint scan = Scan)
    => new(vk, (ushort)scan, (ushort)(up ? NativeMethods.RI_KEY_BREAK : 0), scan);

// These tests use only the old public API, so they also reproduce the v1.0.1 bugs.
Test("Eisu hook down/up pulses must keep one uninterrupted hold", () =>
{
    var states = new List<bool>();
    var tracker = new HoldKeyTracker(new(Eisu, "英数"), 0, states.Add);
    for (var i = 0; i < 6; i++)
    {
        tracker.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, Key());
        tracker.ProcessKeyMessage(NativeMethods.WM_KEYUP, Key(flags: NativeMethods.LLKHF_UP));
    }
    Assert(tracker.IsPressed && states.SequenceEqual(new[] { true }),
        $"hold reset: {string.Join(',', states)}");
});

Test("Eisu Raw Input make/break pulses must keep one uninterrupted hold", () =>
{
    var states = new List<bool>();
    var tracker = new HoldKeyTracker(new(Eisu, "英数"), 60, states.Add);
    for (var i = 0; i < 6; i++)
    {
        tracker.ProcessRawKeyboard(Raw());
        tracker.ProcessRawKeyboard(Raw(up: true));
    }
    Assert(tracker.IsPressed && states.SequenceEqual(new[] { true }),
        $"hold reset: {string.Join(',', states)}");
});

Test("Ordinary modifier keys still release immediately", () =>
{
    foreach (var vk in new[] { NativeMethods.VK_LMENU, NativeMethods.VK_RCONTROL, NativeMethods.VK_LSHIFT })
    {
        var states = new List<bool>();
        var tracker = new HoldKeyTracker(new(vk, "modifier"), 0, states.Add);
        tracker.ProcessKeyMessage(NativeMethods.WM_SYSKEYDOWN, Key(vk));
        tracker.ProcessKeyMessage(NativeMethods.WM_SYSKEYDOWN, Key(vk));
        tracker.ProcessKeyMessage(NativeMethods.WM_SYSKEYUP, Key(vk, flags: NativeMethods.LLKHF_UP));
        Assert(!tracker.IsPressed && states.SequenceEqual(new[] { true, false }), "modifier changed");
    }
});

#if !BASELINE
Fixture New(int delay = 1, int speed = 31, int margin = 0) => new(delay, speed, margin);
void Down(Fixture f, uint vk = Eisu, uint scan = Scan, uint flags = 0)
    => f.Tracker.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, Key(vk, scan, flags));
void Continuous(Fixture f)
    => Assert(f.Tracker.IsPressed && f.States.SequenceEqual(new[] { true }), "unexpected hold/axis reset");

Test("Delayed hook/raw duplicate cannot shorten first-repeat wait", () =>
{
    var f = New();
    Down(f);
    f.Advance(25);
    f.Tracker.ProcessRawKeyboard(Raw());
    f.Advance(400);
    Continuous(f);
    f.Advance(75);
    Down(f);
    Continuous(f);
});

Test("Even early same-source duplicates cannot shorten initial lease", () =>
{
    var f = New();
    Down(f);
    f.Advance(25); Down(f);
    f.Advance(35); Down(f);
    f.Advance(440); Down(f);
    Continuous(f);
});

Test("All 128 Windows delay/speed settings sustain pulsed input and expire", () =>
{
    for (var delay = 0; delay <= 3; delay++)
    for (var speed = 0; speed <= 31; speed++)
    {
        var f = New(delay, speed);
        Down(f);
        f.Tracker.ProcessKeyMessage(NativeMethods.WM_KEYUP, Key(flags: NativeMethods.LLKHF_UP));
        f.Advance(250 + delay * 250 + 20); // Initial-delay scheduling jitter.
        Down(f);
        var period = (int)Math.Ceiling(1000 / (2.5 + speed * 27.5 / 31) * 1.25);
        for (var repeat = 0; repeat < 8; repeat++)
        {
            f.Advance(period);
            f.Tracker.ProcessRawKeyboard(Raw());
            Down(f);
            f.Tracker.ProcessRawKeyboard(Raw(up: true));
            f.Tracker.ProcessKeyMessage(NativeMethods.WM_KEYUP, Key(flags: NativeMethods.LLKHF_UP));
            Continuous(f);
        }
        f.Advance(1200);
        Assert(!f.Tracker.IsPressed && f.States.SequenceEqual(new[] { true, false }),
            $"release failed for delay={delay}, speed={speed}");
    }
});

Test("Slow observed repeats above 120ms and moderate jitter are retained", () =>
{
    var f = New(speed: 10);
    Down(f);
    f.Advance(500); Down(f);
    foreach (var interval in new[] { 150, 180, 170, 200, 195, 190 })
    {
        f.Advance(interval); Down(f); Continuous(f);
    }
    f.Advance(1100);
    Assert(!f.Tracker.IsPressed, "stuck after repeats stopped");
});

Test("Hook/raw ordering and delayed duplicates maintain one hold", () =>
{
    foreach (var rawFirst in new[] { true, false })
    {
        var f = New();
        void Both()
        {
            if (rawFirst) f.Tracker.ProcessRawKeyboard(Raw()); else Down(f);
            f.Advance(27);
            if (rawFirst) Down(f); else f.Tracker.ProcessRawKeyboard(Raw());
            f.Tracker.ProcessRawKeyboard(Raw(up: true));
            f.Tracker.ProcessKeyMessage(NativeMethods.WM_KEYUP, Key(flags: NativeMethods.LLKHF_UP));
        }
        Both();
        f.Advance(473); Both();
        for (var i = 0; i < 20; i++) { f.Advance(6); Both(); Continuous(f); }
        f.Advance(1200);
        Assert(f.States.SequenceEqual(new[] { true, false }), "duplicate transition");
    }
});

Test("Raw-only and hook-only streams both expire after the last repeat", () =>
{
    foreach (var rawOnly in new[] { true, false })
    {
        var f = New();
        void Repeat() { if (rawOnly) f.Tracker.ProcessRawKeyboard(Raw()); else Down(f); }
        Repeat(); f.Advance(500); Repeat();
        for (var i = 0; i < 30; i++) { f.Advance(33); Repeat(); Continuous(f); }
        f.Advance(109); Continuous(f);
        f.Advance(1);
        Assert(!f.Tracker.IsPressed && f.States.SequenceEqual(new[] { true, false }), "deadline not bounded");
    }
});

Test("Initial margin 0 and 60 do not allow key-up to reset the hold", () =>
{
    foreach (var margin in new[] { 0, 60 })
    {
        var f = New(margin: margin);
        Down(f);
        f.Tracker.ProcessKeyMessage(NativeMethods.WM_KEYUP, Key(flags: NativeMethods.LLKHF_UP));
        f.Advance(554 + margin); Continuous(f);
        f.Advance(1);
        Assert(!f.Tracker.IsPressed, "initial lease did not expire");
    }
});

Test("Known Eisu scan code can continue as IME PROCESSKEY", () =>
{
    var f = New();
    Down(f); f.Advance(500); Down(f, NativeMethods.VK_PROCESSKEY);
    for (var i = 0; i < 20; i++)
    {
        f.Advance(33);
        f.Tracker.ProcessRawKeyboard(Raw(vk: (ushort)NativeMethods.VK_PROCESSKEY));
        Down(f, NativeMethods.VK_PROCESSKEY);
        Continuous(f);
    }
    f.Advance(1200);
    Down(f, NativeMethods.VK_PROCESSKEY);
    Assert(!f.Tracker.IsPressed, "expired identity must not start a new hold");
});

Test("Unrelated IME keys, different scan codes and E0 keys cannot keep hold alive", () =>
{
    var f = New();
    Down(f, NativeMethods.VK_PROCESSKEY);
    Assert(!f.Tracker.IsPressed, "unidentified IME key started hold");
    Down(f);
    for (var i = 0; i < 6; i++)
    {
        f.Advance(100);
        Down(f, NativeMethods.VK_PROCESSKEY, scan: 0x1E);
        Down(f, NativeMethods.VK_PROCESSKEY, flags: NativeMethods.LLKHF_EXTENDED);
        Down(f, (uint)'A');
    }
    Assert(!f.Tracker.IsPressed, "other key extended hold");
});

Test("Raw CapsLock repeats renew only the scan code already identified as Eisu", () =>
{
    var f = New();
    f.Tracker.ProcessRawKeyboard(Raw(vk: (ushort)NativeMethods.VK_CAPITAL));
    Assert(!f.Tracker.IsPressed, "CapsLock alone must not start Eisu hold");
    Down(f);
    f.Advance(500);
    for (var i = 0; i < 20; i++)
    {
        f.Tracker.ProcessRawKeyboard(Raw(vk: (ushort)NativeMethods.VK_CAPITAL));
        f.Tracker.ProcessRawKeyboard(Raw(up: true, vk: (ushort)NativeMethods.VK_CAPITAL));
        f.Advance(33);
        Continuous(f);
    }
    f.Advance(1200);
    Assert(!f.Tracker.IsPressed, "raw CapsLock stream did not expire");
});

Test("Zero scan code does not match arbitrary PROCESSKEY", () =>
{
    var f = New();
    Down(f, scan: 0);
    f.Advance(400); Down(f, NativeMethods.VK_PROCESSKEY, scan: 0);
    f.Advance(200);
    Assert(!f.Tracker.IsPressed, "zero identity extended hold");
});

Test("A later valid scan code can identify an initially synthetic down", () =>
{
    var f = New();
    Down(f, scan: 0);
    f.Advance(10); f.Tracker.ProcessRawKeyboard(Raw());
    f.Advance(490); Down(f, NativeMethods.VK_PROCESSKEY);
    f.Advance(33); Down(f, NativeMethods.VK_PROCESSKEY);
    Continuous(f);
});

Test("Eisu shortcut repeats, key-ups and duplicate input fire only once", () =>
{
    long now = 0;
    var shortcut = new EisuHotkeyTracker(0, () => now, 1, 31);
    var fired = 0;
    void Repeat()
    {
        if (shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, Key(), true)) fired++;
        if (shortcut.ProcessRawKeyboard(Raw(), true)) fired++;
        if (shortcut.ProcessKeyMessage(NativeMethods.WM_KEYUP, Key(flags: NativeMethods.LLKHF_UP), true)) fired++;
        if (shortcut.ProcessRawKeyboard(Raw(up: true), true)) fired++;
    }
    Repeat(); now = 500; shortcut.ProcessTimerTick(); Repeat();
    for (var i = 0; i < 50; i++) { now += 33; shortcut.ProcessTimerTick(); Repeat(); }
    Assert(fired == 1, $"shortcut toggled {fired} times");
    now += 1200; shortcut.ProcessTimerTick(); Repeat();
    Assert(fired == 2, "new hold must allow another toggle");
});

Test("Eisu shortcut requires modifiers and keeps existing PROCESSKEY fallback", () =>
{
    long now = 0;
    var shortcut = new EisuHotkeyTracker(0, () => now, 1, 31);
    Assert(!shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, Key(NativeMethods.VK_PROCESSKEY), false), "no modifiers");
    now = 500;
    Assert(shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, Key(NativeMethods.VK_PROCESSKEY), true), "IME fallback lost");
    now += 33;
    Assert(!shortcut.ProcessRawKeyboard(Raw(vk: (ushort)NativeMethods.VK_PROCESSKEY), true), "duplicate toggle");
});

Test("Zero-scan shortcut duplicates are still consumed once", () =>
{
    long now = 0;
    var shortcut = new EisuHotkeyTracker(0, () => now, 1, 31);
    Assert(shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, Key(scan: 0), true), "first toggle missing");
    Assert(!shortcut.ProcessRawKeyboard(Raw(scan: 0), true), "zero scan escaped deduplication");
});

Test("Unrelated IME typing cannot leave an Eisu shortcut latched", () =>
{
    long now = 0;
    var shortcut = new EisuHotkeyTracker(0, () => now, 1, 31);
    Assert(shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, Key(), true), "first toggle missing");
    for (var i = 0; i < 7; i++)
    {
        now += 100; shortcut.ProcessTimerTick();
        Assert(!shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN,
            Key(NativeMethods.VK_PROCESSKEY, scan: 0x1E), false), "unmodified IME key toggled");
    }
    Assert(!shortcut.IsPressed, "unrelated IME key prolonged hold");
    Assert(shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, Key(), true), "next toggle stayed latched");
});

Test("PROCESSKEY-only zero-scan shortcut can repeat without retoggling", () =>
{
    long now = 0;
    var shortcut = new EisuHotkeyTracker(0, () => now, 1, 31);
    var key = Key(NativeMethods.VK_PROCESSKEY, scan: 0);
    Assert(shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, key, true), "first toggle missing");
    now = 500;
    for (var i = 0; i < 50; i++)
    {
        shortcut.ProcessTimerTick();
        Assert(!shortcut.ProcessKeyMessage(NativeMethods.WM_KEYDOWN, key, true), "repeated toggle");
        now += 33;
    }
});
#endif

Console.WriteLine($"{count - failures}/{count} tests passed");
return failures == 0 ? 0 : 1;

#if !BASELINE
sealed class Fixture
{
    public long Now { get; private set; }
    public List<bool> States { get; } = new();
    public HoldKeyTracker Tracker { get; }

    public Fixture(int delay, int speed, int margin)
    {
        Tracker = new HoldKeyTracker(new(NativeMethods.VK_DBE_ALPHANUMERIC, "英数"),
            margin, States.Add, () => Now, delay, speed);
    }

    // Deterministic timer checks; no sleeps, OS hooks, settings writes or ClipCursor.
    public void Advance(int milliseconds)
    {
        for (var i = 0; i < milliseconds; i++) { Now++; Tracker.ProcessTimerTick(); }
    }
}
#endif
