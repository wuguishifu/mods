using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PingSystem
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "wuguishifu.PingSystem";
        public const string PluginName = "PingSystem";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<string> PingKey;
        internal static ConfigEntry<float> PingDuration;
        internal static ConfigEntry<float> MaxDistance;
        internal static ConfigEntry<float> MarkerScale;
        internal static ConfigEntry<float> Cooldown;

        private void Awake()
        {
            Log = Logger;

            PingKey = Config.Bind("Controls", "PingKey", "<Mouse>/middleButton",
                "Input System binding path for the ping key. Examples: <Mouse>/middleButton, " +
                "<Keyboard>/g, <Keyboard>/z, <Mouse>/forwardButton, <Gamepad>/dpad/up");
            PingDuration = Config.Bind("Pings", "PingDuration", 6f,
                new ConfigDescription("Seconds a ping stays visible.", new AcceptableValueRange<float>(1f, 60f)));
            MaxDistance = Config.Bind("Pings", "MaxDistance", 150f,
                new ConfigDescription("How far away (in meters) you can ping.", new AcceptableValueRange<float>(5f, 500f)));
            Cooldown = Config.Bind("Pings", "Cooldown", 0.4f,
                new ConfigDescription("Minimum seconds between your pings.", new AcceptableValueRange<float>(0f, 10f)));
            MarkerScale = Config.Bind("Display", "MarkerScale", 1f,
                new ConfigDescription("Size multiplier for ping markers and text.", new AcceptableValueRange<float>(0.5f, 3f)));

            var go = new GameObject("PingSystem");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<PingManager>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded (key: {PingKey.Value})");
        }
    }

    internal class Ping
    {
        public int Actor;
        public string Owner;
        public Color Color;
        public Vector3 Position;       // world position (used when there is no target)
        public Transform Target;       // object the ping follows, if any
        public Vector3 LocalOffset;    // hit point in the target's local space
        public string FallbackLabel;
        public float SpawnTime;

        public Vector3 WorldPosition =>
            Target != null ? Target.TransformPoint(LocalOffset) : Position;
    }

    internal class PingManager : MonoBehaviour, IOnEventCallback
    {
        private const byte PingEventCode = 147;

        private static readonly Color[] Palette =
        {
            new Color(1.00f, 0.78f, 0.10f), // yellow
            new Color(0.25f, 0.80f, 1.00f), // cyan
            new Color(1.00f, 0.35f, 0.35f), // red
            new Color(0.45f, 1.00f, 0.45f), // green
            new Color(0.85f, 0.50f, 1.00f), // purple
            new Color(1.00f, 0.60f, 0.20f), // orange
            new Color(1.00f, 0.50f, 0.80f), // pink
            new Color(0.95f, 0.95f, 0.95f), // white
        };

        private static readonly AccessTools.FieldRef<ValuableObject, float> ValuableDollarValue =
            AccessTools.FieldRefAccess<ValuableObject, float>("dollarValueCurrent");

        private readonly Dictionary<int, Ping> _pings = new Dictionary<int, Ping>();
        private InputAction _pingAction;
        private float _lastPingTime = -999f;
        private Texture2D _ring;
        private Texture2D _dot;
        private GUIStyle _labelStyle;
        private GUIStyle _subStyle;

        private void Awake()
        {
            BuildAction();
            Plugin.PingKey.SettingChanged += (_, __) => BuildAction();
            _ring = MakeCircleTexture(64, 0.70f, 1.0f);
            _dot = MakeCircleTexture(64, 0f, 0.35f);
        }

        private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
        private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

        private void BuildAction()
        {
            _pingAction?.Disable();
            _pingAction?.Dispose();
            try
            {
                _pingAction = new InputAction("Ping", InputActionType.Button, Plugin.PingKey.Value);
                _pingAction.Enable();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Invalid PingKey '{Plugin.PingKey.Value}': {e.Message}");
                _pingAction = null;
            }
        }

        private static bool InGameplay()
        {
            try
            {
                return SemiFunc.RunIsLevel() || SemiFunc.RunIsShop() || SemiFunc.RunIsLobby() || SemiFunc.RunIsArena();
            }
            catch
            {
                return false;
            }
        }

        private void Update()
        {
            if (!InGameplay())
            {
                if (_pings.Count > 0) _pings.Clear();
                return;
            }

            // Expire old pings
            if (_pings.Count > 0)
            {
                float now = Time.time;
                var expired = new List<int>();
                foreach (var kv in _pings)
                    if (now - kv.Value.SpawnTime > Plugin.PingDuration.Value) expired.Add(kv.Key);
                foreach (var k in expired) _pings.Remove(k);
            }

            if (_pingAction == null || !_pingAction.WasPressedThisFrame()) return;
            if (Time.time - _lastPingTime < Plugin.Cooldown.Value) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;     // pause menu / UI open
            if (!SemiFunc.NoTextInputsActive()) return;                // typing in chat

            TryPing();
        }

        private void TryPing()
        {
            Camera cam = Camera.main;
            PlayerAvatar local = SemiFunc.PlayerAvatarLocal();
            if (cam == null || local == null) return;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, Plugin.MaxDistance.Value,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Transform localRoot = local.transform.root;
            Transform controllerRoot = PlayerController.instance != null ? PlayerController.instance.transform.root : null;

            foreach (RaycastHit hit in hits)
            {
                Transform root = hit.collider.transform.root;
                if (root == localRoot || root == controllerRoot) continue;   // don't ping yourself

                PhotonView view = hit.collider.GetComponentInParent<PhotonView>();
                int viewId = view != null ? view.ViewID : 0;
                string label = Describe(hit.collider.gameObject);
                string owner = SemiFunc.PlayerGetName(local) ?? "Player";

                _lastPingTime = Time.time;
                int actor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 1;
                AddPing(actor, owner, hit.point, viewId, label);

                if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode)
                {
                    object[] payload = { hit.point, viewId, label, owner };
                    PhotonNetwork.RaiseEvent(PingEventCode, payload,
                        new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                        SendOptions.SendReliable);
                }
                return;
            }
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != PingEventCode) return;
            try
            {
                if (!(photonEvent.CustomData is object[] data) || data.Length < 4) return;
                AddPing(photonEvent.Sender, (string)data[3], (Vector3)data[0], (int)data[1], (string)data[2]);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Bad ping event: {e.Message}");
            }
        }

        private void AddPing(int actor, string owner, Vector3 point, int viewId, string label)
        {
            var ping = new Ping
            {
                Actor = actor,
                Owner = owner,
                Color = Palette[Mathf.Abs(actor) % Palette.Length],
                Position = point,
                FallbackLabel = label,
                SpawnTime = Time.time,
            };

            if (viewId != 0)
            {
                PhotonView view = PhotonView.Find(viewId);
                if (view != null)
                {
                    ping.Target = view.transform;
                    ping.LocalOffset = view.transform.InverseTransformPoint(point);
                }
            }

            _pings[actor] = ping;   // one active ping per player; a new ping replaces the old one
        }

        private static string Describe(GameObject go)
        {
            EnemyParent enemy = go.GetComponentInParent<EnemyParent>();
            if (enemy != null)
                return string.IsNullOrEmpty(enemy.enemyName) ? "Enemy" : $"Enemy: {enemy.enemyName}";

            ValuableObject valuable = go.GetComponentInParent<ValuableObject>();
            if (valuable != null)
            {
                int value = Mathf.RoundToInt(ValuableDollarValue(valuable));
                return value > 0 ? $"Valuable ${SemiFunc.DollarGetString(value)}" : "Valuable";
            }

            ItemAttributes item = go.GetComponentInParent<ItemAttributes>();
            if (item != null)
                return item.item != null && !string.IsNullOrEmpty(item.item.itemName) ? item.item.itemName : "Item";

            PlayerAvatar player = go.GetComponentInParent<PlayerAvatar>();
            if (player != null)
                return SemiFunc.PlayerGetName(player) ?? "Player";

            if (go.GetComponentInParent<PhysGrabCart>() != null) return "Cart";
            if (go.GetComponentInParent<ExtractionPoint>() != null) return "Extraction";

            return "Here";
        }

        private string CurrentLabel(Ping ping)
        {
            // Re-describe live so e.g. a valuable's value updates as it takes damage.
            if (ping.Target != null)
            {
                try { return Describe(ping.Target.gameObject); } catch { }
            }
            return ping.FallbackLabel;
        }

        private void OnGUI()
        {
            if (_pings.Count == 0 || Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            float ui = Screen.height / 1080f * Plugin.MarkerScale.Value;
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _subStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            }
            _labelStyle.fontSize = Mathf.RoundToInt(20 * ui);
            _subStyle.fontSize = Mathf.RoundToInt(15 * ui);

            Color prevColor = GUI.color;
            foreach (Ping ping in _pings.Values)
            {
                Vector3 world = ping.WorldPosition;
                Vector3 vp = cam.WorldToViewportPoint(world);
                bool behind = vp.z < 0f;
                if (behind) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }

                float margin = 40f * ui;
                float x = vp.x * Screen.width;
                float y = (1f - vp.y) * Screen.height;
                bool offscreen = behind || x < margin || x > Screen.width - margin || y < margin || y > Screen.height - margin;
                if (behind)
                {
                    // Push behind-camera markers to the screen edge in the right direction.
                    Vector2 dir = new Vector2(x - Screen.width / 2f, y - Screen.height / 2f);
                    if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                    dir.Normalize();
                    x = Screen.width / 2f + dir.x * Screen.width;
                    y = Screen.height / 2f + dir.y * Screen.height;
                }
                x = Mathf.Clamp(x, margin, Screen.width - margin);
                y = Mathf.Clamp(y, margin, Screen.height - margin);

                float age = Time.time - ping.SpawnTime;
                float remaining = Plugin.PingDuration.Value - age;
                float alpha = Mathf.Clamp01(remaining / 1f);           // fade out over the last second
                float pop = 1f + 0.6f * Mathf.Clamp01(1f - age / 0.25f); // pop-in effect

                // Expanding pulse ring right after the ping
                if (age < 0.8f)
                {
                    float t = age / 0.8f;
                    float pr = Mathf.Lerp(16f, 60f, t) * ui;
                    GUI.color = new Color(ping.Color.r, ping.Color.g, ping.Color.b, (1f - t) * alpha);
                    GUI.DrawTexture(new Rect(x - pr, y - pr, pr * 2, pr * 2), _ring);
                }

                float r = 16f * ui * pop * (offscreen ? 0.8f : 1f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
                GUI.DrawTexture(new Rect(x - r - 2, y - r - 2, r * 2 + 4, r * 2 + 4), _ring);
                GUI.color = new Color(ping.Color.r, ping.Color.g, ping.Color.b, alpha);
                GUI.DrawTexture(new Rect(x - r, y - r, r * 2, r * 2), _ring);
                GUI.DrawTexture(new Rect(x - r, y - r, r * 2, r * 2), _dot);

                int meters = Mathf.RoundToInt(Vector3.Distance(cam.transform.position, world));
                string label = CurrentLabel(ping);
                string sub = $"{ping.Owner} · {meters}m";

                float w = 400f * ui;
                float lh = 26f * ui;
                float top = y - r - lh * 2.1f;
                if (top < 4f) top = y + r + 4f;   // flip text below marker near the top edge

                DrawShadowed(new Rect(x - w / 2, top, w, lh), label, _labelStyle, new Color(1f, 1f, 1f, alpha), alpha);
                DrawShadowed(new Rect(x - w / 2, top + lh, w, lh), sub, _subStyle,
                    new Color(ping.Color.r, ping.Color.g, ping.Color.b, alpha), alpha);
            }
            GUI.color = prevColor;
        }

        private static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color, float alpha)
        {
            GUI.color = Color.white;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        // Builds a white anti-aliased ring/disc texture: pixels between inner and outer radius (0..1) are opaque.
        private static Texture2D MakeCircleTexture(int size, float inner, float outer)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) / 2f;
            float aa = 1.5f / c;
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float d = Mathf.Sqrt((px - c) * (px - c) + (py - c) * (py - c)) / c;
                float a = Mathf.Clamp01((outer - d) / aa);
                if (inner > 0f) a *= Mathf.Clamp01((d - inner) / aa);
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}
