using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using UnityEngine;

namespace SeoulFirstWeek.Quest
{
    /// <summary>
    /// A way in from 명동길: a building's door (pressed, E or a click) or the subway's stairs (walked down into).
    /// Only the current level's entrance is open; a closed station exit shows its "under repair" barrier.
    /// </summary>
    public class BuildingEntrance : Interactable
    {
        public string entranceId;
        public string sceneName;
        public string nameKo;
        public Color color = Color.red;
        public Transform exitPoint;
        public Transform approach; // where a level starts: down the street, facing this door
        public ObjectiveBeacon beacon;
        [Tooltip("Entered by walking into the trigger (down the station stairs) instead of pressing E at a door.")]
        public bool walkIn;
        [Tooltip("Shown while the entrance is closed (the barrier across a station exit's stairs).")]
        public GameObject closedBarrier;

        bool open = true, entering;
        BoxCollider box;
        Transform player;

        public bool Open
        {
            get => open;
            set
            {
                open = value;
                if (closedBarrier) closedBarrier.SetActive(!value);
            }
        }

        public override string Prompt => Loc.T("enter", nameKo);
        public override bool CanInteract => base.CanInteract && Open && !walkIn;

        void Awake() => box = GetComponent<BoxCollider>();

        void Update()
        {
            if (!walkIn || !open || entering || !box) return;
            if (!player)
            {
                var fpc = FindAnyObjectByType<FirstPersonController>();
                if (!fpc) return;
                player = fpc.transform;
            }
            if (box.bounds.Contains(player.position + Vector3.up * 0.5f)) Interact(null);
        }

        public override void Interact(PlayerInteractor player)
        {
            if (entering) return;
            entering = walkIn;
            Sfx.Play(ProceduralAudio.Chime, 0.4f);
            SceneFlow.Load(sceneName);
        }
    }
}
