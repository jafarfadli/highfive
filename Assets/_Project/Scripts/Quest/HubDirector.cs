using System.Linq;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using SeoulFirstWeek.UI;
using UnityEngine;

namespace SeoulFirstWeek.Quest
{
    /// <summary>
    /// Start of a level: the background story on a black screen, then the Myeongdong street with the
    /// player down the road from the level's building. Only that door is open and marked.
    /// </summary>
    public class HubDirector : MonoBehaviour
    {
        public FirstPersonController player;
        public Transform spawnPoint;
        public BuildingEntrance[] entrances;

        void Start()
        {
            var session = GameSession.I;
            var level = session.EnsureLevel();
            var target = entrances.FirstOrDefault(e => e.entranceId == level.entrance) ?? entrances[0];

            var spot = target.approach ? target.approach : spawnPoint;
            var d = target.transform.position - spot.position;
            player.Teleport(spot.position, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);

            foreach (var e in entrances)
            {
                bool isTarget = e == target;
                e.Open = isTarget;
                if (e.beacon) e.beacon.gameObject.SetActive(isTarget);
                if (isTarget && e.beacon) e.beacon.SetState(false, true);
            }

            var hud = Hud.Create();
            PauseMenu.Create(player);
            CursorState.Gameplay(true);
            var place = level.Place;
            var accent = place != null ? place.Color : target.color;
            hud.SetQuest(level.title.Get(), $"{target.nameKo} · {Loc.T("part_n", level.level)}", accent);
            hud.SetProgress(0, 1);
            hud.SetTask(Loc.T("go_to", target.nameKo));

            void Begin()
            {
                hud.ShowHelp(Hud.ControlsHelp, 15f);
                if (!string.IsNullOrEmpty(level.needs) && !session.Has(level.needs))
                    Toasts.Warn(Loc.T("needs_doc", Documents.LocalName(level.needs.Replace("doc:", ""))), null, 7f);
            }

            var story = level.story?.Get();
            if (string.IsNullOrEmpty(story)) Begin();
            else StoryCard.Show(player, story, Begin);
        }
    }
}
