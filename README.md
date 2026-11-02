# FDTrace

**FoxDeployTrace**: ultra-speed, ultra-scale log viewing. Built to outpace CMTrace
with three tricks:

- **Compact indexing:** one text buffer, four bytes per line; no eager row copies.
- **Virtualized rows:** create UI items on demand, with a bounded 512-row cache.
- **Lazy formatting:** highlight and clean requested rows, not the entire log.

Saved highlight rules, full-row details, optional PID/TID and null-GUID hiding.

**Local benchmark:** 10 MiB / 41,776 rows opened in 146 ms; ~20.2 MiB additional
managed memory. Synthetic test, not a head-to-head CMTrace comparison.

```powershell
.\artifacts\FDTrace.exe "C:\Logs\gc_agent.log"
```

Windows + .NET 8 Desktop Runtime. Keep the published files together.
