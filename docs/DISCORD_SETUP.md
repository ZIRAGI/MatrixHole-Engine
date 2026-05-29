# Discord Authentication Setup

## 1. Create Discord Application

1. Go to https://discord.com/developers/applications
2. Click **New Application** → name it "MatrixHole Auth"
3. Go to **OAuth2** → **General**
4. Add Redirect URI:
   ```
   http://localhost:53134/auth/discord
   ```
5. Copy **Client ID** and **Client Secret**

## 2. Configure AppSecrets

Put credentials into `%LocalAppData%/MatrixHole/app_secrets.json`:
```json
{
  "DiscordClientId": "YOUR_CLIENT_ID",
  "DiscordClientSecret": "YOUR_CLIENT_SECRET",
  "RequiredGuildId": "YOUR_GUILD_ID",
  "AllowedRoleIds": ["ROLE_ID_1", "ROLE_ID_2"]
}
```

## 3. Discord Bot (Optional, for role checking)

If you want the app to verify Discord server membership automatically:

1. Go to **Bot** tab → **Add Bot**
2. Enable intents: `Server Members Intent`
3. Copy **Bot Token** — add to `app_secrets.json` as `DiscordBotToken`
4. Invite bot to your server:
   ```
   https://discord.com/oauth2/authorize?client_id=YOUR_ID&scope=bot&permissions=268435456
   ```

## 4. How It Works

1. User clicks "Connect Discord" in the app
2. App opens browser to Discord OAuth URL with `identity+guilds` scopes
3. Discord redirects to `localhost:53134` with auth code
4. App exchanges code for access token
5. App queries Discord API for user's guilds
6. If user is in `RequiredGuildId` with one of `AllowedRoleIds` → access granted

## 5. Required OAuth Scopes

```
identify guilds guilds.members.read
```

## 6. Role Verification API

```bash
GET https://discord.com/api/v10/users/@me/guilds/{guild_id}/member
Authorization: Bearer {access_token}
```

Response contains `roles` array — check against `AllowedRoleIds`.

## 7. Troubleshooting

| Issue | Solution |
|-------|----------|
| `redirect_uri` mismatch | Check exact URI in Discord dev portal matches the one in code |
| `invalid_client` | Client Secret expired — regenerate in Discord portal |
| Bot can't see roles | Bot needs `Server Members Intent` + must be above verified roles in hierarchy |
| Localhost not working | Windows Firewall may block port 53134 — add exception |

## 8. Disable Discord Gate (For Testing)

In `app.js`, comment out the `checkDiscordAccess()` call or set:
```javascript
localStorage.setItem('discord_authed', 'true');
```
