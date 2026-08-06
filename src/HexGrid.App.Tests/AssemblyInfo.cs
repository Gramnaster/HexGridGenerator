// Every test class in this assembly spins up its own real WinForms Control on a dedicated STA
// thread. xUnit parallelizes across test classes by default, which lets those threads create window
// handles concurrently - first-time initialization of process-wide WinForms/GDI+ state (SystemEvents'
// hidden window, Control.DefaultFont) from multiple independent STA apartments at once is a known
// source of intermittent flakiness. The suite is small (33 tests, ~1.5s), so serializing it costs
// nothing measurable in exchange for determinism.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
