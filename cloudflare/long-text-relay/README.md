# TextCrate Long Text Relay Worker

This Cloudflare Worker backs TextCrate's optional Long Text Relay feature.

The Worker never receives plaintext. TextCrate encrypts with AES-GCM locally and uploads only ciphertext, nonce, optional password salt, expiry time, burn-after-read metadata, and opaque random tokens.

## Threat Model

Cloudflare can see:

- Client IP addresses and request timing.
- The opaque token path used to fetch encrypted metadata.
- Ciphertext size.
- Expiry, burn-after-read flag, password-protected flag, and KDF iteration count.

Cloudflare cannot see:

- Plaintext.
- The fragment decryption key.
- The optional password.
- The final AES-GCM content key.

Anyone with the full URL, including its fragment key, can fetch and decrypt the encrypted payload until it expires or burns. If a password was set, the link and password are both required. Burn-after-read deletes only after the browser successfully decrypts and calls the burn endpoint with a separate fragment burn token.

## Public URL Shape

One-time URLs use:

```text
https://<obscure-subdomain>.ghostkernel.cc/x/<high-entropy-token>#k=<key>&b=<burn-token>
```

Current app builds use a shorter compact fragment:

```text
https://<obscure-subdomain>.ghostkernel.cc/x/<high-entropy-token>#<key>.<burn-token>
```

The `/x/` path and token are non-enumerable. Avoid obvious subdomains or paths such as `paste`, `clip`, `clipboard`, `share`, or `relay`.

The receiving page also shows an optional PowerShell command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "iex (irm https://qz9v4k.ghostkernel.cc/.gk7/ps1); Receive-TextCrateRelay '<full-one-time-url>'"
```

That helper still decrypts locally. The Worker-served script contains no plaintext or secret and the full URL fragment is passed only to local PowerShell.

## KV

KV is used because relay items are short-to-medium text snippets and KV supports TTL-based expiry. R2 is unnecessary unless you want much larger payloads later.

Create namespaces:

```powershell
cd cloudflare\long-text-relay
npx wrangler kv namespace create RELAY_KV
npx wrangler kv namespace create RELAY_KV --preview
```

Copy the returned IDs into `wrangler.toml`.

## Deploy With Wrangler

```powershell
cd cloudflare\long-text-relay
npm install
npm test
npx wrangler deploy
```

The checked-in configuration uses this obscure custom domain:

```toml
routes = [{ pattern = "qz9v4k.ghostkernel.cc", custom_domain = true }]
```

Use that full origin as the TextCrate Settings endpoint, for example:

```text
https://qz9v4k.ghostkernel.cc
```

## Deploy With Cloudflare MCP

The same pieces can be created through the Cloudflare MCP for the `gtvanrooyen` account:

1. Create a KV namespace named `textcrate-long-text-relay`.
2. Deploy `src/index.ts` as a Worker with a `RELAY_KV` binding.
3. Add a custom domain under `ghostkernel.cc`.
4. Keep `MAX_CIPHERTEXT_BYTES`, `RATE_LIMIT_PER_MINUTE`, and `RATE_LIMIT_PER_HOUR` at the values from `wrangler.toml` unless you deliberately tune them.

The app fails closed when the endpoint is blank, disabled, unreachable, or returns an error.

## Local Config

Copy `.dev.vars.example` to `.dev.vars` only for local overrides. Do not commit `.dev.vars`.

## Tests

```powershell
npm test
```

Tests cover expiry validation, burn-after-read, password metadata handling, token errors, and encrypted metadata retrieval.
