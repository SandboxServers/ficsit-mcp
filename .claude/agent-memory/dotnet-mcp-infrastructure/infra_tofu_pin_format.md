---
name: infra-tofu-pin-format
description: TOFU certificate pin format — SHA-256 hash, keyed by authority (host:port), atomic GetOrPin
metadata:
  type: project
---

The dedicated-server TLS trust-on-first-use (TOFU) pin store format and contract:

- **Pin value = SHA-256** cert hash via `certificate.GetCertHashString(HashAlgorithmName.SHA256)` —
  NOT the collision-prone SHA-1 `X509Certificate2.Thumbprint`. Comparison is normalized (no
  separators/whitespace, upper-invariant) and case-insensitive.
- **Pin key = authority (`host:port`)** via `Uri.Authority`, NOT `Uri.Host`. Two services on the same
  host at different ports must not be confused. `FileCertificatePinStore.NormalizeHost`
  lower-invariants the whole `host:port` string (port digits unaffected).
- **First-contact pinning is atomic** via `ICertificatePinStore.GetOrPin(host, thumbprint)`: returns
  the EFFECTIVE pin (existing if present, else stores and returns the offered value) under the store's
  write lock; persists only when it actually adds. A lost first-contact race becomes a deterministic
  mismatch instead of two writers clobbering each other.
- **Pin file location:** default `%LocalAppData%/ficsit-mcp/cert-pins.json`
  (`FileCertificatePinStore.DefaultPinFilePath`). Overridable via `DedicatedServerOptions.CertPinFilePath`
  (env `FICSITMCP_DedicatedServer__CertPinFilePath`), resolved at pin-store singleton construction —
  containers/read-only deploys should point this at a mounted writable path.
- **Dev escape hatch:** `DedicatedServerOptions.DangerousAcceptAnyCert` (env
  `FICSITMCP_DedicatedServer__DangerousAcceptAnyCert`) accepts any cert WITHOUT pinning. Dev only.

**Why:** SHA-1 is collision-prone and unsuitable as a security anchor; host-only keys conflate
co-located services; check-then-pin was a TOCTOU race. No migration needed switching to SHA-256
because no pins existed yet.

**How to apply:** any code touching pins must use `GetOrPin` for first contact and key by authority.
Related: [[infra-retry-opt-in]].
