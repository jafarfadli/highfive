using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using UnityEngine;

namespace SeoulFirstWeek.Quest
{
    /// <summary>Door on the Myeongdong street. Only the current level's door is open.</summary>
    public class BuildingEntrance : Interactable
    {
        public string entranceId;
        public string sceneName;
        public string nameKo;
        public Color color = Color.red;
        public Transform exitPoint;
        public Transform approach; // where a level starts: down the street, facing this door
        public ObjectiveBeacon beacon;

        public bool Open { get; set; } = true;

        public override string Prompt => Loc.T("enter", nameKo);
        public override bool CanInteract => base.CanInteract && Open;

        public override void Interact(PlayerInteractor player)
        {
            Sfx.Play(ProceduralAudio.Chime, 0.4f);
            SceneFlow.Load(sceneName);
        }
    }
}
