# FDTrace

![FDTrace header](src/image/fdTraceHeader.png)

**FoxDeployTrace**: ultra-speed, ultra-scale log viewing. Built to outpace CMTrace
with a few clever tricks:

- **Compact indexing:** one text buffer with small allocations!
- **Virtualized rows:** these seem good!
- **Lazy formatting:** hide random garbage in logs!!

Features saved and default highlight rules, full-row details, optional PID/TID and null-GUID hiding.

**Local benchmark:** 10 MiB / 41,776 rows opened in 146 ms; ~20.2 MiB additional
managed memory. Synthetic test, not a head-to-head CMTrace comparison.

```powershell
.\artifacts\portable\win-x64\FDTrace.exe "C:\Logs\gc_agent.log"
```

Requires the **.NET 8 Desktop Runtime (x64)**.

Build: `dotnet publish .\src\FoxDeployTrace -c Release -p:PublishProfile=Portable`.