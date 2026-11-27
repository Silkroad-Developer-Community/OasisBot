using System;
using Python.Runtime;
using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Core.PlayerConfigProfile;
using RSBot.Python.Runtime;
using RSBot.Python.Views;

namespace RSBot.Python.API.Core
{
    public class TrainingAPI : IPythonPlugin
    {
        /// <summary>
        /// Unique module name of the plugin.
        /// </summary>
        public string ModuleName => "training";

        public void Init(PythonContext context) { }

        public bool set_training_area(string name)
        {
            var areas = PlayerConfig.Profile?.Training?.Areas;
            if (areas == null)
                return false;

            var targetArea = areas.Find(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (targetArea == null)
            {
                Log.Notify($"Training area '{name}' not found.");
                return false;
            }
            foreach (var area in areas)
                area.IsActive = false;

            targetArea.IsActive = true;
            PlayerConfig.Save();
            EventManager.FireEvent("OnSetTrainingArea");
            return true;
        }

        public bool set_training_position(float x, float y, ushort region, int radius = 30)
        {
            if (Game.Player == null || Kernel.Bot?.Botbase == null)
                return false;

            try
            {
                Position pos = new(x, y, region);

                var trainingArea = PlayerConfig.Profile.Training.Areas.Find(a => a.IsActive);

                trainingArea.Region = pos.Region.Id;
                trainingArea.X = pos.XOffset;
                trainingArea.Y = pos.YOffset;
                trainingArea.Z = pos.ZOffset;
                trainingArea.Radius = radius;

                PlayerConfig.Save();

                Log.Notify(
                    "New training area coordinates set. "
                        + $"X: {pos.XOffset}, Y: {pos.YOffset}, Z: {pos.ZOffset}, Region: {pos.Region}"
                );
                EventManager.FireEvent("OnSetTrainingArea");

                return true;
            }
            catch (Exception e)
            {
                Log.Notify($"An error occurred while trying to set training position: {e}");
                return false;
            }
        }

        public bool set_training_script(string path)
        {
            if (PlayerConfig.Profile.Training == null)
                return false;

            PlayerConfig.Profile.Training.WalkbackFile = path;
            PlayerConfig.Save();
            return true;
        }

        public PyDict get_training_area(string name)
        {
            using (Py.GIL())
            {
                var areas = PlayerConfig.Profile?.Training?.Areas;
                if (areas != null)
                {
                    foreach (var area in areas)
                    {
                        if (area == null)
                            continue;
                        if (area.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            var regionName = Game.ReferenceManager.GetTranslation(area.Region.ToString());
                            return PythonData.Dict(
                                ("name", area.Name),
                                ("x", area.X),
                                ("y", area.Y),
                                ("z", area.Z),
                                ("radius", area.Radius),
                                ("region", (int)area.Region),
                                ("region_name", regionName)
                            );
                        }
                    }
                }
                return new PyDict();
            }
        }

        public PyList get_training_areas()
        {
            using (Py.GIL())
            {
                var areas = PlayerConfig.Profile?.Training?.Areas;
                if (areas != null)
                {
                    var result = new PyList();
                    foreach (var area in areas)
                    {
                        if (area == null)
                            continue;
                        var regionName = Game.ReferenceManager.GetTranslation(area.Region.ToString());
                        result.Append(
                            PythonData.Dict(
                                ("name", area.Name),
                                ("x", area.X),
                                ("y", area.Y),
                                ("z", area.Z),
                                ("radius", area.Radius),
                                ("region", (int)area.Region),
                                ("region_name", regionName),
                                ("active", area.IsActive)
                            )
                        );
                    }
                    return result;
                }
                return new PyList();
            }
        }

        public PyDict get_training_position()
        {
            using (Py.GIL())
            {
                if (Game.Player == null || Kernel.Bot?.Botbase?.Area == null)
                    return new PyDict();
                var area = Kernel.Bot.Botbase.Area;
                var regionName = Game.ReferenceManager.GetTranslation(area.Position.Region.ToString());
                return PythonData.Dict(
                    ("name", area.Name),
                    ("x", area.Position.X),
                    ("y", area.Position.Y),
                    ("z", area.Position.ZOffset),
                    ("radius", area.Radius),
                    ("region", (int)area.Position.Region.Id),
                    ("region_name", regionName)
                );
            }
        }

        public bool move_to(float x, float y, ushort region)
        {
            if (Game.Player == null)
                return false;
            try
            {
                Position pos = new(x, y, region);
                Game.Player.MoveTo(pos, false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string get_training_script()
        {
            return PlayerConfig.Profile.Training?.WalkbackFile ?? string.Empty;
        }
    }
}
