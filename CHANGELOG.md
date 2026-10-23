# Prism.SSGI changelog

## 2.2.3 — 2026-10-23

- Prepare Vulkan uniforms from portable render callbacks and restore temporal rejection.
- Copy the final enabled spatial-filter result, or Temporal at zero iterations, into one stable color-history owner across scale, disable and DX11 handoff.
- Add production adapter and GPU history-copy regressions.

## 2.2.2 — 2026-10-15

- Disable guest temporal, moments, a-trous, composite, and history stages on DX11 while preserving the shared trace; native SVGF exclusively owns DX11 denoising/history.
- Retry missing companion discovery, report initialization and lookup failures in logs and Status, and stop retrying ordinary initialization failures for the session.
- Preserve propagation of genuine DXGI device failures and distinguish non-DX11 routes from successful initialization.

## 2.2.1 — 2026-10-14

- Rebind the DX11 companion to the live Pulsar core rather than creating a second settings identity.
- Build matched companions with isolated intermediates and valid game access grants; add the Cecil dependency required for source-identity rebinding.
- Register a renderer-neutral Status page independently of the settings page and include terminal registration health in diagnostic reports.

## 2.2.0 — previous release

Release 2.2.0 SVGF denoise and Vulkan guest passes

Version: 2.1.1 -> 2.2.0 (minor).

Changelog:
- Add a half-resolution SVGF chain after the trace: temporal color, moments, four a-trous passes, previous depth, and previous normal.
- Composite that result with DirectAdd into world HDR.
- Run the same chain on the Vulkan guest bus. The DX11 SVGF host is Prism.SSGI.Dx11 and loads only on Windows DX11.

Co-authored-by: Cursor <cursoragent@cursor.com>
