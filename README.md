# JonsBlog2

## Admin login configuration

The admin username is stored in `JonsBlog2/appsettings.json` under `AdminLogin:Username`.

The admin password hash is stored in **.NET User Secrets** during local development under `AdminLogin:PasswordHash` so it does not need to be committed to the repo.

Set the current password hash with:

```bash
dotnet user-secrets set "AdminLogin:PasswordHash" "PBKDF2$SHA256$210000$IhIU2XbLEyaUAVWbC6of9g==$L/3OeD4zCHLIgC0nkq7tlf7hFpLRPu3lROLEPJW99gI=" --project JonsBlog2/JonsBlog2.csproj
```

To generate a new hash for a replacement password:

```bash
python3 - <<'PY'
import os, base64, hashlib
password = input('New admin password: ').encode()
iterations = 210000
salt = os.urandom(16)
subkey = hashlib.pbkdf2_hmac('sha256', password, salt, iterations, dklen=32)
print(f"PBKDF2$SHA256${iterations}${base64.b64encode(salt).decode()}${base64.b64encode(subkey).decode()}")
PY
```

Then store the printed value with:

```bash
dotnet user-secrets set "AdminLogin:PasswordHash" "<paste-generated-hash-here>" --project JonsBlog2/JonsBlog2.csproj
```

To verify what is currently stored in local development:

```bash
dotnet user-secrets list --project JonsBlog2/JonsBlog2.csproj
```

