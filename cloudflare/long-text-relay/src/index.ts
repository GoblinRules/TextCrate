export interface Env {
  RELAY_KV: KVNamespace;
  MAX_CIPHERTEXT_BYTES?: string;
  RATE_LIMIT_PER_MINUTE?: string;
  RATE_LIMIT_PER_HOUR?: string;
}

type StoredItem = {
  ciphertext: string;
  nonce: string;
  salt?: string;
  expiresAt: number;
  burnAfterRead: boolean;
  passwordProtected: boolean;
  kdfIterations: number;
  burnTokenHash: string;
};

type UploadRequest = {
  token?: string;
  ciphertext?: string;
  nonce?: string;
  salt?: string | null;
  expiryMinutes?: number;
  burnAfterRead?: boolean;
  passwordProtected?: boolean;
  kdfIterations?: number;
  burnToken?: string;
  burnTokenHash?: string;
};

type ShortLink = {
  url: string;
  expiresAt: number;
};

type ShortenRequest = {
  url?: string;
  expiryMinutes?: number;
};

const TOKEN_RE = /^[A-Za-z0-9_-]{20,96}$/;
const SHORT_RE = /^[A-Za-z0-9_-]{8,32}$/;
const B64URL_RE = /^[A-Za-z0-9_-]+$/;
const GENERIC_UNAVAILABLE = 'This link is expired, burned, unavailable, or invalid.';

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    try {
      if (request.method === 'GET' && url.pathname === '/.gk7/status') {
        return json({ ok: true });
      }

      if (request.method === 'POST' && url.pathname === '/.gk7/store') {
        return await handleStore(request, env);
      }

      if (request.method === 'POST' && url.pathname === '/.gk7/shorten') {
        return await handleShorten(request, env, url.origin);
      }

      if (request.method === 'GET' && url.pathname.startsWith('/.gk7/item/')) {
        return await handleItem(url.pathname.slice('/.gk7/item/'.length), env);
      }

      if (request.method === 'POST' && url.pathname === '/.gk7/burn') {
        return await handleBurn(request, env);
      }

      if (request.method === 'GET' && url.pathname.startsWith('/x/')) {
        return html(receiverPage(url.pathname.slice('/x/'.length)));
      }

      if (request.method === 'GET' && url.pathname.startsWith('/s/')) {
        return await handleShortLink(url.pathname.slice('/s/'.length), env);
      }

      return new Response('Not found', { status: 404 });
    } catch {
      return json({ error: 'Request failed.' }, 400);
    }
  }
};

async function handleShorten(request: Request, env: Env, origin: string): Promise<Response> {
  if (!await allowRequest(request, env)) {
    return json({ error: 'Too many requests.' }, 429);
  }

  const body = await request.json<ShortenRequest>();
  if (typeof body.url !== 'string' || typeof body.expiryMinutes !== 'number') {
    return json({ error: 'Invalid request.' }, 400);
  }

  const target = new URL(body.url);
  if (target.origin !== origin || !target.pathname.startsWith('/x/') || target.hash.length < 8) {
    return json({ error: 'Invalid request.' }, 400);
  }

  const expiryMinutes = Math.max(1, Math.min(60, Math.floor(body.expiryMinutes)));
  const code = base64Url(crypto.getRandomValues(new Uint8Array(8)));
  const expiresAt = Date.now() + expiryMinutes * 60_000;
  await env.RELAY_KV.put(shortKey(code), JSON.stringify({ url: target.toString(), expiresAt } satisfies ShortLink), {
    expirationTtl: expiryMinutes * 60
  });

  return json({ url: `${origin}/s/${code}`, expiresAt });
}

async function handleShortLink(code: string, env: Env): Promise<Response> {
  if (!SHORT_RE.test(code)) {
    return unavailable();
  }

  const item = await env.RELAY_KV.get(shortKey(code), 'json') as ShortLink | null;
  if (!item || item.expiresAt <= Date.now()) {
    await env.RELAY_KV.delete(shortKey(code));
    return unavailable();
  }

  return new Response(null, {
    status: 302,
    headers: {
      location: item.url,
      'cache-control': 'no-store'
    }
  });
}

async function handleStore(request: Request, env: Env): Promise<Response> {
  if (!await allowRequest(request, env)) {
    return json({ error: 'Too many requests.' }, 429);
  }

  const contentLength = Number(request.headers.get('content-length') ?? '0');
  if (contentLength > maxCiphertextBytes(env) + 8192) {
    return json({ error: 'Request too large.' }, 413);
  }

  const body = await request.json<UploadRequest>();
  if (!isValidUpload(body, env)) {
    return json({ error: 'Invalid request.' }, 400);
  }

  const expiryMinutes = Math.max(1, Math.min(60, Math.floor(body.expiryMinutes!)));
  const expiresAt = Date.now() + expiryMinutes * 60_000;
  const item: StoredItem = {
    ciphertext: body.ciphertext!,
    nonce: body.nonce!,
    salt: body.salt ?? undefined,
    expiresAt,
    burnAfterRead: body.burnAfterRead !== false,
    passwordProtected: body.passwordProtected === true,
    kdfIterations: body.kdfIterations ?? 310000,
    burnTokenHash: body.burnTokenHash ?? await sha256Base64Url(body.burnToken!)
  };

  await env.RELAY_KV.put(itemKey(body.token!), JSON.stringify(item), {
    expirationTtl: expiryMinutes * 60
  });

  return json({ ok: true, expiresAt });
}

async function handleItem(token: string, env: Env): Promise<Response> {
  if (!TOKEN_RE.test(token)) {
    return unavailable();
  }

  const item = await readItem(token, env);
  if (!item) {
    return unavailable();
  }

  return json({
    ciphertext: item.ciphertext,
    nonce: item.nonce,
    salt: item.salt ?? null,
    expiresAt: item.expiresAt,
    burnAfterRead: item.burnAfterRead,
    passwordProtected: item.passwordProtected,
    kdfIterations: item.kdfIterations
  });
}

async function handleBurn(request: Request, env: Env): Promise<Response> {
  const body = await request.json<{ token?: string; burnToken?: string }>();
  if (!body.token || !body.burnToken || !TOKEN_RE.test(body.token) || !B64URL_RE.test(body.burnToken)) {
    return unavailable();
  }

  const item = await readItem(body.token, env);
  if (!item) {
    return unavailable();
  }

  if (await sha256Base64Url(body.burnToken) === item.burnTokenHash) {
    await env.RELAY_KV.delete(itemKey(body.token));
  }

  return json({ ok: true });
}

async function readItem(token: string, env: Env): Promise<StoredItem | null> {
  const value = await env.RELAY_KV.get(itemKey(token), 'json') as StoredItem | null;
  if (!value || value.expiresAt <= Date.now()) {
    await env.RELAY_KV.delete(itemKey(token));
    return null;
  }

  return value;
}

function isValidUpload(body: UploadRequest, env: Env): boolean {
  const expiry = body.expiryMinutes;
  return typeof body.token === 'string'
    && TOKEN_RE.test(body.token)
    && typeof body.ciphertext === 'string'
    && B64URL_RE.test(body.ciphertext)
    && body.ciphertext.length <= Math.ceil(maxCiphertextBytes(env) * 4 / 3) + 8
    && typeof body.nonce === 'string'
    && B64URL_RE.test(body.nonce)
    && (!body.salt || B64URL_RE.test(body.salt))
    && typeof expiry === 'number'
    && expiry >= 1
    && expiry <= 60
    && (
      typeof body.burnTokenHash === 'string' && B64URL_RE.test(body.burnTokenHash)
      || typeof body.burnToken === 'string' && B64URL_RE.test(body.burnToken)
    );
}

async function allowRequest(request: Request, env: Env): Promise<boolean> {
  const ip = request.headers.get('cf-connecting-ip') ?? 'unknown';
  const minute = Math.floor(Date.now() / 60_000);
  const hour = Math.floor(Date.now() / 3_600_000);
  const minuteLimit = Number(env.RATE_LIMIT_PER_MINUTE ?? '30');
  const hourLimit = Number(env.RATE_LIMIT_PER_HOUR ?? '300');
  const [minuteCount, hourCount] = await Promise.all([
    incrementCounter(env, `rate:m:${minute}:${ip}`, 90),
    incrementCounter(env, `rate:h:${hour}:${ip}`, 3900)
  ]);
  return minuteCount <= minuteLimit && hourCount <= hourLimit;
}

async function incrementCounter(env: Env, key: string, ttlSeconds: number): Promise<number> {
  const current = Number(await env.RELAY_KV.get(key) ?? '0') + 1;
  await env.RELAY_KV.put(key, String(current), { expirationTtl: ttlSeconds });
  return current;
}

async function sha256Base64Url(value: string): Promise<string> {
  const data = new TextEncoder().encode(value);
  const hash = await crypto.subtle.digest('SHA-256', data);
  return base64Url(new Uint8Array(hash));
}

function base64Url(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}

function itemKey(token: string): string {
  return `i:${token}`;
}

function shortKey(code: string): string {
  return `s:${code}`;
}

function maxCiphertextBytes(env: Env): number {
  return Number(env.MAX_CIPHERTEXT_BYTES ?? '262144');
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'cache-control': 'no-store'
    }
  });
}

function html(body: string): Response {
  return new Response(body, {
    headers: {
      'content-type': 'text/html; charset=utf-8',
      'cache-control': 'no-store',
      'content-security-policy': "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'"
    }
  });
}

function unavailable(): Response {
  return json({ error: GENERIC_UNAVAILABLE }, 404);
}

function receiverPage(token: string): string {
  const safeToken = TOKEN_RE.test(token) ? token : '';
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>TextCrate</title>
<style>
body{margin:0;font-family:Segoe UI,system-ui,sans-serif;background:#0f172a;color:#e5eefc}
main{max-width:760px;margin:9vh auto;padding:24px}
button,input{font:inherit}
.box{border:1px solid #334155;background:#111827;padding:20px}
.muted{color:#b6c7df}
textarea{width:100%;min-height:240px;background:#020617;color:#e5eefc;border:1px solid #475569;padding:12px;box-sizing:border-box}
button{background:#22d3ee;color:#082f49;border:0;padding:10px 16px;margin-right:8px;cursor:pointer}
input{background:#020617;color:#e5eefc;border:1px solid #475569;padding:9px;width:280px;max-width:100%;margin-right:8px}
#error{color:#fecaca}
</style>
</head>
<body>
<main>
<h1>TextCrate</h1>
<div class="box">
<p class="muted">Decrypts in this browser. The key and password are not sent to Cloudflare.</p>
<div id="passwordRow" hidden><input id="password" type="password" autocomplete="off" placeholder="Password"><button id="unlock">Unlock</button></div>
<p id="status">Loading...</p>
<p id="error"></p>
<textarea id="text" readonly hidden></textarea>
<p><button id="copy" hidden>Copy full text</button></p>
</div>
</main>
<script>
const token=${JSON.stringify(safeToken)};
const statusEl=document.getElementById('status');
const errorEl=document.getElementById('error');
const textEl=document.getElementById('text');
const copyBtn=document.getElementById('copy');
const passwordRow=document.getElementById('passwordRow');
const passwordInput=document.getElementById('password');
const unlockBtn=document.getElementById('unlock');
let item;
const fail=(m)=>{statusEl.textContent='';errorEl.textContent=m||'This link is expired, burned, unavailable, or invalid.'};
const b64=(s)=>Uint8Array.from(atob(s.replaceAll('-','+').replaceAll('_','/').padEnd(s.length+(4-s.length%4)%4,'=')),c=>c.charCodeAt(0));
const enc=new TextEncoder();
const b64u=(bytes)=>{let s='';for(const b of bytes)s+=String.fromCharCode(b);return btoa(s).replaceAll('+','-').replaceAll('/','_').replaceAll('=','')};
async function finalKey(master,salt,password,iterations){
  // The fragment key is random. If a password is present, PBKDF2 adds a second local-only factor.
  if(!password) return await crypto.subtle.importKey('raw',master,'AES-GCM',false,['decrypt']);
  const passKey=await crypto.subtle.importKey('raw',new TextEncoder().encode(password),'PBKDF2',false,['deriveBits']);
  const bits=await crypto.subtle.deriveBits({name:'PBKDF2',hash:'SHA-256',salt,iterations},passKey,256);
  const combined=new Uint8Array(master.length+32);
  combined.set(master,0); combined.set(new Uint8Array(bits),master.length);
  const digest=await crypto.subtle.digest('SHA-256',combined);
  combined.fill(0);
  return await crypto.subtle.importKey('raw',digest,'AES-GCM',false,['decrypt']);
}
async function decrypt(password){
  try{
    const hash=parseFragment();
    const k=hash.k;
    if(!k) return fail('Missing decryption key.');
    const key=await finalKey(b64(k), item.salt ? b64(item.salt) : new Uint8Array(), password || '', item.kdfIterations || 310000);
    const plain=await crypto.subtle.decrypt({name:'AES-GCM',iv:b64(item.nonce),tagLength:128},key,b64(item.ciphertext));
    textEl.value=new TextDecoder().decode(plain);
    textEl.hidden=false; copyBtn.hidden=false; passwordRow.hidden=true; errorEl.textContent=''; statusEl.textContent='Ready.';
    if(item.burnAfterRead){
      const burnToken=hash.b || await deriveBurnToken(k);
      if(burnToken) fetch('/.gk7/burn',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({token,burnToken})}).catch(()=>{});
    }
  }catch{ fail('Could not decrypt. Check the link and password.'); }
}
async function deriveBurnToken(k){
  const master=b64(k);
  const prefix=enc.encode('TextCrate burn token v1');
  const material=new Uint8Array(prefix.length+master.length);
  material.set(prefix,0); material.set(master,prefix.length);
  const hash=await crypto.subtle.digest('SHA-256',material);
  material.fill(0); master.fill(0);
  return b64u(new Uint8Array(hash));
}
(async()=>{
  if(!token) return fail();
  const res=await fetch('/.gk7/item/'+encodeURIComponent(token),{cache:'no-store'});
  if(!res.ok) return fail();
  item=await res.json();
  if(item.passwordProtected){statusEl.textContent='Password required.';passwordRow.hidden=false;passwordInput.focus();}
  else await decrypt('');
})();
function parseFragment(){
  const raw=location.hash.slice(1);
  if(raw.includes('=')){
    const p=new URLSearchParams(raw);
    return {k:p.get('k'),b:p.get('b')};
  }
  const parts=raw.split('.');
  return {k:parts[0]||null,b:parts[1]||null};
}
unlockBtn.onclick=()=>decrypt(passwordInput.value);
passwordInput.onkeydown=(e)=>{if(e.key==='Enter') decrypt(passwordInput.value)};
copyBtn.onclick=async()=>{await navigator.clipboard.writeText(textEl.value); statusEl.textContent='Copied.'};
</script>
</body>
</html>`;
}
