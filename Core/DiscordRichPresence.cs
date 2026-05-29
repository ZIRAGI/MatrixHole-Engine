using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// DiscordRichPresence — интеграция с Discord via IPC (п.54 OptimizatorPlan).
    /// Показывает: играет в SCP:SL, сервер, роль, время в игре.
    /// </summary>
    public static class DiscordRichPresence
    {
        private static bool _initialized;
        private static string? _currentState;
        private static string? _currentDetails;
        private static long? _startTimestamp;
        private static string? _currentLargeImage;
        private static string? _currentLargeText;
        private static string? _currentSmallImage;
        private static string? _currentSmallText;
        private static string? _currentPartyId;
        private static int _partySize;
        private static int _partyMax;

        // Discord IPC pipe name
        private const string DiscordPipeName = "discord-ipc-0";

        public static string Initialize()
        {
            try
            {
                _initialized = true;
                _startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return "{\"ok\":true}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string SetActivity(string state, string details, string? largeImage = null, string? largeText = null,
            string? smallImage = null, string? smallText = null, string? partyId = null, int partySize = 0, int partyMax = 0)
        {
            if (!_initialized) Initialize();
            _currentState = state;
            _currentDetails = details;
            _currentLargeImage = largeImage;
            _currentLargeText = largeText;
            _currentSmallImage = smallImage;
            _currentSmallText = smallText;
            _currentPartyId = partyId;
            _partySize = partySize;
            _partyMax = partyMax;

            try
            {
                SendPresence();
                return "{\"ok\":true}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string UpdateGamePresence(string serverName, string role, int players, int maxPlayers)
        {
            return SetActivity(
                state: $"На сервере: {serverName}",
                details: $"Роль: {role}",
                largeImage: "scpsl_logo",
                largeText: "SCP: Secret Laboratory",
                smallImage: role.ToLower().Replace(" ", "_"),
                smallText: role,
                partyId: $"scpsl_{serverName.GetHashCode()}",
                partySize: players,
                partyMax: maxPlayers
            );
        }

        public static string ClearPresence()
        {
            try
            {
                _currentState = null;
                _currentDetails = null;
                SendPresence(clear: true);
                return "{\"ok\":true}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string GetCurrentPresenceJson()
        {
            return JsonConvert.SerializeObject(new
            {
                initialized = _initialized,
                state = _currentState,
                details = _currentDetails,
                startTimestamp = _startTimestamp,
                largeImage = _currentLargeImage,
                largeText = _currentLargeText,
                smallImage = _currentSmallImage,
                smallText = _currentSmallText,
                partyId = _currentPartyId,
                partySize = _partySize,
                partyMax = _partyMax
            });
        }

        private static void SendPresence(bool clear = false)
        {
            // Try to send via Discord IPC pipe
            try
            {
                using var client = new NamedPipeClientStream(".", DiscordPipeName, PipeDirection.InOut);
                client.Connect(500);

                var activity = new
                {
                    state = clear ? null : _currentState,
                    details = clear ? null : _currentDetails,
                    timestamps = clear ? null : new { start = _startTimestamp },
                    assets = clear ? null : new
                    {
                        large_image = _currentLargeImage,
                        large_text = _currentLargeText,
                        small_image = _currentSmallImage,
                        small_text = _currentSmallText
                    },
                    party = (clear || string.IsNullOrEmpty(_currentPartyId)) ? null : new
                    {
                        id = _currentPartyId,
                        size = new[] { _partySize, _partyMax }
                    }
                };

                var payload = new
                {
                    cmd = "SET_ACTIVITY",
                    args = new
                    {
                        pid = Process.GetCurrentProcess().Id,
                        activity
                    },
                    nonce = Guid.NewGuid().ToString()
                };

                var json = JsonConvert.SerializeObject(payload);
                var bytes = Encoding.UTF8.GetBytes(json);
                var header = new byte[8];
                header[0] = 1; // opcode: FRAME
                BitConverter.GetBytes(bytes.Length).CopyTo(header, 4);

                client.Write(header, 0, 8);
                client.Write(bytes, 0, bytes.Length);
            }
            catch { /* Discord not running or pipe unavailable */ }
        }
    }
}
