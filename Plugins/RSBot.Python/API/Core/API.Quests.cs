using System.Collections.Generic;
using System.Linq;
using Python.Runtime;
using RSBot.Core;
using RSBot.Core.Objects.Quests;
using RSBot.Python.Runtime;

namespace RSBot.Python.API.Core
{
    public class QuestAPI : IPythonPlugin
    {
        /// <summary>
        /// Unique module name of the plugin.
        /// </summary>
        public string ModuleName => "quest";

        public void Init(PythonContext context) { }

        private string GetStatusText(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.Cancelled:
                    return "Cancelled";
                case QuestStatus.Completed:
                case QuestStatus.CompletedXTimes:
                    return "Completed";
                case QuestStatus.CompletedButNotSupplied:
                case QuestStatus.CompletedByUserButNotSupplied:
                    return "Ready for delivery";
                case QuestStatus.Initialized:
                case QuestStatus.StartedByUser:
                    return "In progress";
                case QuestStatus.Unavailable:
                    return "Unavailable";
                default:
                    return status.ToString();
            }
        }

        public PyList get_quests()
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (Game.Player == null)
                    return result;

                foreach (var activeQuest in Game.Player.QuestLog.ActiveQuests)
                {
                    var pyQuest = new PyDict();
                    var quest = activeQuest.Value;
                    var npcs = new List<PyDict>();
                    var rewardItems = new List<PyDict>();
                    var objectives = new List<PyDict>();
                    var rewardExp = 0;
                    var rewardSp = 0;
                    var rewardGold = 0;
                    if (quest.Npcs?.Length > 0)
                    {
                        foreach (var npcId in quest.Npcs)
                        {
                            var npc = Game.ReferenceManager.GetRefObjChar(npcId);
                            npcs.Append(
                                PythonData.Dict(
                                    ("npc_name", npc.NameStrID),
                                    ("npc_id", npc.ID),
                                    ("npc_servername", npc.CodeName)
                                )
                            );
                        }
                    }
                    if (quest.Quest.Reward != null)
                    {
                        if (quest.Quest.Reward.Exp > 0)
                            rewardExp = quest.Quest.Reward.Exp;

                        if (quest.Quest.Reward.SP > 0)
                            rewardSp = quest.Quest.Reward.SP;

                        if (quest.Quest.Reward.Gold > 0)
                            rewardGold = quest.Quest.Reward.Gold;
                    }

                    if (quest.Quest.RewardItems != null && quest.Quest.RewardItems.Any())
                    {
                        foreach (var rewardItem in quest.Quest.RewardItems)
                        {
                            rewardItems.Append(
                                PythonData.Dict(
                                    ("reward_item", rewardItem.Item?.GetRealName()),
                                    ("reward_optional", rewardItem.OptionalItem?.GetRealName())
                                )
                            );
                        }
                    }

                    if (quest.Objectives?.Length > 0)
                        foreach (var objective in quest.Objectives)
                        {
                            var objectiveName = Game.ReferenceManager.GetTranslation(objective.NameStrId);
                            foreach (var task in objective.Tasks)
                            {
                                var actualTitle = objectiveName.Replace("%d", task.ToString());
                                objectives.Append(PythonData.Dict(("objective", actualTitle)));
                            }
                        }
                    result.Append(
                        PythonData.Dict(
                            ("name", Game.ReferenceManager.GetTranslation(quest.Quest.NameString)),
                            ("id", quest.Id),
                            ("level", quest.Quest.Level),
                            ("status", GetStatusText(quest.Status)),
                            ("notice_npc", Game.ReferenceManager.GetTranslation(quest.Quest.NoticeNPC)),
                            ("npcs", PythonData.List(npcs)),
                            ("reward_exp", rewardExp),
                            ("reward_sp", rewardSp),
                            ("reward_gold", rewardGold),
                            ("reward_items", PythonData.List(rewardItems)),
                            ("objectives", PythonData.List(objectives))
                        )
                    );
                }

                return result;
            }
        }
    }
}
