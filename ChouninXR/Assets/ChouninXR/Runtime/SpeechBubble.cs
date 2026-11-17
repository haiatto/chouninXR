using TMPro;
using UnityEngine;

namespace ChouninXR
{
    // 町人の頭上に出るセリフウィンドウ（ワールド空間）。
    // 見た目はプラットフォームで変えられるよう Style で受け取る
    // （例：AURA は光学シースルーなので黒が透ける → 明るい文字＋半透明の暗い板は効きにくい）。
    public class SpeechBubble : MonoBehaviour
    {
        [System.Serializable]
        public class Style
        {
            public Color panelColor = new Color(1f, 1f, 1f, 0.92f);
            public Color textColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            public float width = 0.9f;
            public float fontSize = 0.075f;
            public float padding = 0.05f;
        }

        public Style style = new Style();

        private ChouninActor _actor;
        private TextMeshPro _text;
        private Transform _panel;
        private Material _panelMaterial;
        private Camera _camera;

        public static SpeechBubble Attach(ChouninActor actor, float height, Style style = null)
        {
            var go = new GameObject("SpeechBubble");
            go.transform.SetParent(actor.transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var bubble = go.AddComponent<SpeechBubble>();
            if (style != null)
            {
                bubble.style = style;
            }
            bubble.Bind(actor);
            return bubble;
        }

        private void Bind(ChouninActor actor)
        {
            _actor = actor;
            Build();
            _actor.LineShown += OnLineShown;
            _actor.TalkEnded += OnTalkEnded;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_actor != null)
            {
                _actor.LineShown -= OnLineShown;
                _actor.TalkEnded -= OnTalkEnded;
            }
            if (_panelMaterial != null)
            {
                Destroy(_panelMaterial);
            }
        }

        private void Build()
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "Panel";
            Destroy(panel.GetComponent<Collider>());
            _panel = panel.transform;
            _panel.SetParent(transform, false);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _panelMaterial = new Material(shader);
            _panelMaterial.color = style.panelColor;
            if (style.panelColor.a < 1f && _panelMaterial.HasProperty("_Surface"))
            {
                // URP Unlit を半透明にする
                _panelMaterial.SetFloat("_Surface", 1f);
                _panelMaterial.SetFloat("_Blend", 0f);
                _panelMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _panelMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _panelMaterial.SetInt("_ZWrite", 0);
                _panelMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            // 文字（TMP, Transparent=3000）より必ず先に描く
            _panelMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
            panel.GetComponent<MeshRenderer>().sharedMaterial = _panelMaterial;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            _text = textGo.AddComponent<TextMeshPro>();
            var font = JapaneseFont.Get();
            if (font != null)
            {
                _text.font = font;
            }
            _text.fontSize = style.fontSize * 10f; // TMP は 1unit=10pt 相当
            _text.color = style.textColor;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.rectTransform.sizeDelta = new Vector2(style.width - style.padding * 2f, 1f);
        }

        private void OnLineShown(ChouninActor actor, string line)
        {
            gameObject.SetActive(true);
            _text.text = line;
            _text.ForceMeshUpdate();
            float h = Mathf.Max(_text.preferredHeight, style.fontSize) + style.padding * 2f;
            _text.rectTransform.sizeDelta = new Vector2(style.width - style.padding * 2f, h - style.padding * 2f);
            _panel.localScale = new Vector3(style.width, h, 1f);
            // 吹き出しの下端が頭の上に来るように
            _panel.localPosition = new Vector3(0f, h * 0.5f, 0f);
            _text.transform.localPosition = new Vector3(0f, h * 0.5f, -0.002f);
        }

        private void OnTalkEnded(ChouninActor actor)
        {
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            // 常にカメラの方を向く（Y 軸回りだけ回すと首を振ったときに読みやすい）
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }
            var dir = transform.position - _camera.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }
}
