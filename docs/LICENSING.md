# Licensing System — Online vs Offline

## Architecture

The app uses **HWID binding** + **HMAC signature validation**.

```
License Key → HMAC(HWID + issued + expires + tier, secret) → Signature
```

The signature is embedded in the license file. On startup:
1. Read encrypted license file
2. Decrypt and parse JSON
3. Compute expected signature from current HWID
4. Compare stored signature with computed
5. Check expiration timestamp

## Online Mode (Recommended for Sales)

### How it works
```
[User] → enters key → [App] → POST /activate {hwid, key} → [Your Server]
[Server] → checks DB → returns {expires_at, tier, signature}
[App] → stores license locally
```

### Server Requirements
- Any VPS ($5-10/month): DigitalOcean, Hetzner, AWS Lightsail
- HTTPS endpoint with JSON API
- SQLite/PostgreSQL database

### Minimal Server Stack (Node.js + SQLite)

```javascript
// server.js — Express + SQLite
const express = require('express');
const sqlite3 = require('sqlite3');
const crypto = require('crypto');

const db = new sqlite3.Database('./licenses.db');
db.run(`CREATE TABLE IF NOT EXISTS licenses (
  key TEXT PRIMARY KEY,
  hwid TEXT,
  tier TEXT DEFAULT 'standard',
  expires_at INTEGER,
  activated_at INTEGER,
  activations INTEGER DEFAULT 0
)`);

const SECRET = process.env.LICENSE_SECRET; // 32+ chars

function sign(hwid, issued, expires, tier) {
  const data = `${hwid}:${issued}:${expires}:${tier}`;
  return crypto.createHmac('sha256', SECRET).update(data).digest('hex').slice(0, 32);
}

const app = express();
app.use(express.json());

app.post('/license/activate', (req, res) => {
  const { hwid, key, version } = req.body;
  
  db.get('SELECT * FROM licenses WHERE key = ?', [key], (err, row) => {
    if (!row) return res.json({ ok: false, error: 'Key not found' });
    if (row.hwid && row.hwid !== hwid) return res.json({ ok: false, error: 'HWID mismatch' });
    if (row.activations >= 3) return res.json({ ok: false, error: 'Max activations reached' });
    
    const now = Math.floor(Date.now() / 1000);
    const expires = row.expires_at || (now + 30 * 86400); // 30 days default
    const tier = row.tier || 'standard';
    
    db.run('UPDATE licenses SET hwid = ?, activated_at = ?, activations = activations + 1 WHERE key = ?',
      [hwid, now, key]);
    
    res.json({ ok: true, expires_at: expires, tier, signature: sign(hwid, now, expires, tier) });
  });
});

app.listen(3000);
```

### Generating Keys (Server-side)

```javascript
function generateKey() {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let key = '';
  for (let i = 0; i < 4; i++) {
    for (let j = 0; j < 4; j++) key += chars[Math.floor(Math.random() * chars.length)];
    if (i < 3) key += '-';
  }
  return key;
}

// Insert into DB
db.run('INSERT INTO licenses (key, tier, expires_at) VALUES (?, ?, ?)',
  [generateKey(), 'pro', Math.floor(Date.now()/1000) + 365*86400]);
```

### Admin Dashboard (Optional)

Simple web panel to:
- Generate keys in bulk (CSV export)
- View activation stats
- Ban/revoke keys
- View HWIDs per key

## Offline Mode (For Piracy-Resistant Distribution)

### How it works
No server needed. Keys are pre-generated using a **master secret**.

```
Key = HMAC(master_secret, HWID + tier + date)
```

User provides HWID → you generate key → user enters key → app validates locally.

### Pros
- Zero server costs
- Works without internet
- Simple to deploy

### Cons
- **Crackable** — reverse engineer `KeySeed` in `LicenseManager.cs`, generate keygen
- No revocation capability
- No activation limits
- No usage analytics

### When to use
- Small closed beta (<100 users)
- Friends & private community
- Testing before server deployment

## Hybrid Mode (Recommended)

1. App validates **offline first** (local signature check)
2. If internet available → pings server for:
   - Key revocation status
   - License extension
   - Tier upgrades
3. If server unreachable → offline validation still works (grace period)

This gives reliability + security:
- Users can use app offline
- You can revoke keys remotely
- Pirates can't simply block a domain

## Security Hardening

### 1. Obfuscate KeySeed

Don't store plaintext secret in code:
```csharp
// BAD
private static readonly byte[] KeySeed = Encoding.UTF8.GetBytes("MH_2025_LICENSE_V1");

// BETTER — build at runtime from fragments
private static byte[] GetKeySeed() {
    var a = new[] { 0x4D, 0x48 }; // "MH"
    var b = new[] { 0x5F, 0x32, 0x30, 0x32, 0x35 }; // "_2025"
    var c = new[] { 0x5F, 0x4C, 0x49, 0x43 }; // "_LIC"
    return a.Concat(b).Concat(c).Select(x => (byte)(x ^ 0x55)).ToArray();
}
```

### 2. Add Anti-Debug to License Check

```csharp
if (IsDebuggerPresent()) return false;
if (CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, out bool dbg) && dbg) return false;
```

### 3. VM/Sandbox Detection

Check for VM artifacts before validating license — prevents automated cracking:
```csharp
// Already in SecurityManager.IsVmDetected()
```

### 4. Code Signing

Sign your EXE with a code signing certificate:
- Standard OV: ~$70-200/year (Sectigo, DigiCert)
- EV: ~$300-700/year (removes SmartScreen warnings)

Without code signing, Windows Defender will likely flag your injector as malware (false positive from heuristics).

## HWID Reset Policy

Users will inevitably upgrade hardware. Have a policy:

| Tier | HWID Resets | How |
|------|-------------|-----|
| Standard | 1 per month | Email support with old + new HWID |
| Pro | 3 per month | Self-service via web panel |
| Lifetime | Unlimited | Automatic |

## Recommended Pricing Model

| Tier | Price | Features |
|------|-------|----------|
| Free | $0 | Basic launcher, no injector |
| Standard | $5/mo or $25/yr | Injector + basic patches |
| Pro | $10/mo or $50/yr | All patches + auto-updater + priority support |
| Lifetime | $99 | One-time, all future updates |

## Quick Start Checklist

- [ ] Decide: Online / Offline / Hybrid
- [ ] Set up license server (or use offline generator)
- [ ] Generate first batch of keys
- [ ] Test activation on clean VM
- [ ] Test HWID change scenario
- [ ] Set up payment processor (Stripe, PayPal, crypto)
- [ ] Create Discord/Telegram for support
