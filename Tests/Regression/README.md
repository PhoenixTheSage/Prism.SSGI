# SSGI regression probes

Run on Windows with the configured Space Engineers/Pulsar paths:

```powershell
dotnet run --project Tests/Regression/SSGI.Regression.csproj -c Release -- "T:\Cursor Projects\Prism.SSGI"
```

The config tests link unmodified production config source and exercise legacy migration, all Low slider fields and persistence. Temporary config files have unique names and are removed; player config is never accessed.

The build extracts the actual lifecycle and catalog-clear methods into a tracking fixture. It checks resource ownership, publication invalidation, repeated resizing/reloading, partial allocation/compile failure and device recovery. Managers, compilation and callbacks are stand-ins: this is a state/resource-contract regression test, not a real game device-reset test.

The shader probe extracts the production visibility-sector and firefly functions and runs their edge cases on D3D11 WARP. It also executes the complete production temporal `ps()` through a compute wrapper using the production file compiler: coherent new light over mature black history, a sparse hit, a bright outlier, and invalid first-frame history. Whole active pixel shaders must also compile with FXC. Actual scenes and hardware GPU timing remain separate acceptance work.
