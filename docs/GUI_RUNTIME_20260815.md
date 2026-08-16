# GUI Runtime / Browser-first Web

DIP is delivered to end users as a hosted web application. Caddy terminates HTTPS and routes same-origin API/SignalR traffic to `Dip.Api`; all other routes go to `Dip.Web`. PostgreSQL, Redis, analysis and Worker remain private.

`Dip.Web` and `Dip.Api` process forwarded protocol/host headers so OAuth callbacks and secure cookies use the public HTTPS origin. Developer verification remains available through `scripts/Verify-GuiRuntime.ps1` and `scripts/Verify-GuiPhase1.ps1`; source presence is not runtime proof.
