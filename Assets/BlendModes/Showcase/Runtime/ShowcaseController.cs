using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Moves the camera between stations and runs the HUD.
    // Keys: Left/Right or A/D to move, Space for Blend on/off. Other keys (F...) go to the current station.
    public class ShowcaseController : MonoBehaviour
    {
        public Camera cam;
        public List<Station> stations = new List<Station>();
        [Tooltip("Seconds the camera takes to glide to the next station.")]
        public float moveTime = 0.35f;

        [Header("HUD")]
        public Button prevButton;
        public Button nextButton;
        public Button blendButton;
        public TMP_Text blendButtonLabel;
        public TMP_Text counterLabel;
        public GameObject opacityGroup;
        public Slider opacitySlider;
        public TMP_Text opacityLabel;

        static readonly KeyCode[] StationKeys = { KeyCode.F, KeyCode.R, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

        int _index;
        Vector3 _velocity;

        public int Index => _index;
        public Station Current => stations.Count > 0 ? stations[_index] : null;

        void Start()
        {
            if (prevButton != null) prevButton.onClick.AddListener(Prev);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (blendButton != null) blendButton.onClick.AddListener(ToggleBlend);
            if (opacitySlider != null) opacitySlider.onValueChanged.AddListener(OnOpacitySlider);

            foreach (var s in stations) if (s != null) s.EnsureBuilt();
            GoTo(0, instant: true);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Next();
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Prev();
            if (Input.GetKeyDown(KeyCode.Space)) ToggleBlend();
            foreach (var k in StationKeys)
                if (Input.GetKeyDown(k) && Current != null) Current.OnKey(k);

            if (cam != null && Current != null)
            {
                var target = Target(Current);
                cam.transform.position = Vector3.SmoothDamp(cam.transform.position, target, ref _velocity, moveTime);
            }
        }

        void LateUpdate()
        {
            // Clicked buttons keep keyboard focus, and Space would "click" them again. Drop the focus.
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
            RefreshHud();
        }

        public void Next() => GoTo(_index + 1);
        public void Prev() => GoTo(_index - 1);

        public void ToggleBlend()
        {
            if (Current == null) return;
            Current.SetBlend(!Current.blendOn);
            RefreshHud();
        }

        public void GoTo(int index, bool instant = false)
        {
            if (stations.Count == 0) return;
            _index = Mathf.Clamp(index, 0, stations.Count - 1);
            for (int i = 0; i < stations.Count; i++)
                if (stations[i] != null) stations[i].SetFocused(i == _index);
            if (instant && cam != null) { cam.transform.position = Target(Current); _velocity = Vector3.zero; }
            if (opacitySlider != null) opacitySlider.SetValueWithoutNotify(Current.opacity);
            RefreshHud();
        }

        // True once the camera has (almost) arrived at the current station.
        public bool Arrived => cam != null && Current != null && Vector3.Distance(cam.transform.position, Target(Current)) < 0.01f;

        Vector3 Target(Station s)
        {
            var p = s.transform.position;
            return new Vector3(p.x, p.y, cam.transform.position.z);
        }

        void OnOpacitySlider(float value)
        {
            if (Current != null) Current.SetOpacity(value);
        }

        void RefreshHud()
        {
            var s = Current;
            if (s == null) return;
            if (counterLabel != null) counterLabel.text = $"{_index + 1} / {stations.Count}\n<size=65%>{s.title}</size>";
            if (blendButtonLabel != null) blendButtonLabel.text = s.blendOn ? "Blend: ON  (Space)" : "Blend: OFF  (Space)";
            if (opacityGroup != null && opacityGroup.activeSelf != s.HasOpacity) opacityGroup.SetActive(s.HasOpacity);
            if (opacitySlider != null && !Mathf.Approximately(opacitySlider.value, s.opacity)) opacitySlider.SetValueWithoutNotify(s.opacity);
            if (opacityLabel != null) opacityLabel.text = $"Opacity {Mathf.RoundToInt(s.opacity * 100f)}%";
        }
    }
}
