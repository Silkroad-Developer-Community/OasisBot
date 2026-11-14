using System.IO;
using System.Media;
using System.Text.RegularExpressions;

namespace RSBot.Core.Objects
{
    /// <summary>
    /// Used for different sound notifications
    /// </summary>
    public class NotificationSounds
    {
        #region Unique in range

        /// <summary>
        ///    Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayAlarmUniqueInRange = false;

        /// <summary>
        ///     Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathAlarmUniqueInRange = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerAlarmUniqueInRange;

        #endregion

        #region General unique appeared

        /// <summary>
        ///    Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayUniqueAlarmGeneral = false;

        /// <summary>
        /// Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathUniqueAlarmGeneral = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerUniqueAlarmGeneral;

        /// <summary>
        /// Regex for unique alarm
        /// </summary>
        public Regex RegexUniqueAlarmGeneral;

        #endregion

        #region Tiger Girl

        /// <summary>
        ///    Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayUniqueAlarmTigerGirl = false;

        /// <summary>
        /// Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathUniqueAlarmTigerGirl = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerUniqueAlarmTigerGirl;

        #endregion

        #region Cerberus

        /// <summary>
        ///    Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayUniqueAlarmCerberus = false;

        /// <summary>
        /// Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathUniqueAlarmCerberus = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerUniqueAlarmCerberus;

        #endregion

        #region Captain Ivy

        /// <summary>
        ///    Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayUniqueAlarmCaptainIvy = false;

        /// <summary>
        /// Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathUniqueAlarmCaptainIvy = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerUniqueAlarmIvy;

        #endregion

        #region Uruchi

        /// <summary>
        ///    Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayUniqueAlarmUruchi = false;

        /// <summary>
        /// Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathUniqueAlarmUruchi = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerUniqueAlarmUruchi;

        #endregion

        #region Isyutaru

        /// <summary>
        ///    Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayUniqueAlarmIsyutaru = false;

        /// <summary>
        /// Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathUniqueAlarmIsyutaru = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerUniqueAlarmIsyutaru;

        #endregion

        #region Isyutaru

        /// <summary>
        ///     Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayUniqueAlarmLordYarkan = false;

        /// <summary>
        /// Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathUniqueAlarmLordYarkan = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerUniqueAlarmLordYarkan;

        #endregion

        #region Demon Shaitan

        /// <summary>
        ///     Auxiliary variable to play sound if unique appeared.
        /// </summary>
        public bool PlayUniqueAlarmDemonShaitan = false;

        /// <summary>
        /// Auxiliary variable path to sound file (.wav)
        /// </summary>
        public string PathUniqueAlarmDemonShaitan = string.Empty;

        /// <summary>
        ///     Player to play sound if unqiue appeared
        /// </summary>
        private SoundPlayer _playerUniqueAlarmDemonShaitan;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public NotificationSounds() { }

        /// <summary>
        ///     Plays sound if unique spotted
        /// </summary>
        public void PlayUniqueInRange()
        {
            if (PlayAlarmUniqueInRange)
            {
                // Play sound
                _playerAlarmUniqueInRange?.Play();
            }
        }

        /// <summary>
        ///     Plays sound if unique appeared
        /// </summary>
        /// <param name="name">Name of unique</param>
        public void PlayUniqueAppeared(string name)
        {
            // Tiger Girl
            if (PlayUniqueAlarmTigerGirl)
            {
                if (Regex.IsMatch(name, "Tiger Girl", RegexOptions.IgnoreCase))
                {
                    _playerUniqueAlarmTigerGirl?.Play();
                    return;
                }
            }

            // Cerberus
            if (PlayUniqueAlarmCerberus)
            {
                if (Regex.IsMatch(name, "Cerberus", RegexOptions.IgnoreCase))
                {
                    _playerUniqueAlarmCerberus?.Play();
                    return;
                }
            }

            // Captain Ivy
            if (PlayUniqueAlarmCaptainIvy)
            {
                if (Regex.IsMatch(name, "Captain Ivy", RegexOptions.IgnoreCase))
                {
                    _playerUniqueAlarmIvy?.Play();
                    return;
                }
            }

            // Uruchi
            if (PlayUniqueAlarmUruchi)
            {
                if (Regex.IsMatch(name, "Uruchi", RegexOptions.IgnoreCase))
                {
                    _playerUniqueAlarmUruchi?.Play();
                    return;
                }
            }

            // Isyutaru
            if (PlayUniqueAlarmIsyutaru)
            {
                if (Regex.IsMatch(name, "Isyutaru", RegexOptions.IgnoreCase))
                {
                    _playerUniqueAlarmIsyutaru?.Play();
                    return;
                }
            }

            // Lord Yarkan
            if (PlayUniqueAlarmLordYarkan)
            {
                if (Regex.IsMatch(name, "Lord Yarkan", RegexOptions.IgnoreCase))
                {
                    _playerUniqueAlarmLordYarkan?.Play();
                    return;
                }
            }

            // Demon Shaitan
            if (PlayUniqueAlarmDemonShaitan)
            {
                if (Regex.IsMatch(name, "Demon Shaitan", RegexOptions.IgnoreCase))
                {
                    _playerUniqueAlarmDemonShaitan?.Play();
                    return;
                }
            }

            // General
            if (PlayUniqueAlarmGeneral)
            {
                if (RegexUniqueAlarmGeneral.IsMatch(name))
                {
                    _playerUniqueAlarmGeneral?.Play();
                }
            }
        }

        /// <summary>
        ///     Loads all player settings regarding notification sounds
        /// </summary>
        public void LoadPlayerSettings()
        {
            // Unique in range
            PlayAlarmUniqueInRange = PlayerConfig.Profile.Alarms.PlayAlarmUniqueInRange;
            PathAlarmUniqueInRange = PlayerConfig.Profile.Alarms.AlarmPathUniqueInRange;
            if (false == string.IsNullOrWhiteSpace(PathAlarmUniqueInRange))
            {
                if (File.Exists(PathAlarmUniqueInRange))
                {
                    _playerAlarmUniqueInRange ??= new();
                    _playerAlarmUniqueInRange.SoundLocation = PathAlarmUniqueInRange;
                }
            }

            // Tiger Girl
            PlayUniqueAlarmTigerGirl = PlayerConfig.Profile.Alarms.PlayTigerGirl;
            PathUniqueAlarmTigerGirl = PlayerConfig.Profile.Alarms.AlarmPathTigerGirl;
            if (false == string.IsNullOrWhiteSpace(PathUniqueAlarmTigerGirl))
            {
                if (File.Exists(PathUniqueAlarmTigerGirl))
                {
                    _playerUniqueAlarmTigerGirl ??= new();
                    _playerUniqueAlarmTigerGirl.SoundLocation = PathUniqueAlarmTigerGirl;
                }
            }

            // Cerberus
            PlayUniqueAlarmCerberus = PlayerConfig.Profile.Alarms.PlayCerberus;
            PathUniqueAlarmCerberus = PlayerConfig.Profile.Alarms.AlarmPathCerberus;
            if (false == string.IsNullOrWhiteSpace(PathUniqueAlarmCerberus))
            {
                if (File.Exists(PathUniqueAlarmCerberus))
                {
                    _playerUniqueAlarmCerberus ??= new();
                    _playerUniqueAlarmCerberus.SoundLocation = PathUniqueAlarmCerberus;
                }
            }

            // Captain Ivy
            PlayUniqueAlarmCaptainIvy = PlayerConfig.Profile.Alarms.PlayCaptainIvy;
            PathUniqueAlarmCaptainIvy = PlayerConfig.Profile.Alarms.AlarmPathCaptainIvy;
            if (false == string.IsNullOrWhiteSpace(PathUniqueAlarmCaptainIvy))
            {
                if (File.Exists(PathUniqueAlarmCaptainIvy))
                {
                    _playerUniqueAlarmIvy ??= new();
                    _playerUniqueAlarmIvy.SoundLocation = PathUniqueAlarmCaptainIvy;
                }
            }

            // Uruchi
            PlayUniqueAlarmUruchi = PlayerConfig.Profile.Alarms.PlayUruchi;
            PathUniqueAlarmUruchi = PlayerConfig.Profile.Alarms.AlarmPathUruchi;
            if (false == string.IsNullOrWhiteSpace(PathUniqueAlarmUruchi))
            {
                if (File.Exists(PathUniqueAlarmUruchi))
                {
                    _playerUniqueAlarmUruchi ??= new();
                    _playerUniqueAlarmUruchi.SoundLocation = PathUniqueAlarmUruchi;
                }
            }

            // Isyutaru
            PlayUniqueAlarmIsyutaru = PlayerConfig.Profile.Alarms.PlayIsyutaru;
            PathUniqueAlarmIsyutaru = PlayerConfig.Profile.Alarms.AlarmPathIsyutaru;
            if (false == string.IsNullOrWhiteSpace(PathUniqueAlarmIsyutaru))
            {
                if (File.Exists(PathUniqueAlarmIsyutaru))
                {
                    _playerUniqueAlarmIsyutaru ??= new();
                    _playerUniqueAlarmIsyutaru.SoundLocation = PathUniqueAlarmIsyutaru;
                }
            }

            // Lord Yarkan
            PlayUniqueAlarmLordYarkan = PlayerConfig.Profile.Alarms.PlayLordYarkan;
            PathUniqueAlarmLordYarkan = PlayerConfig.Profile.Alarms.AlarmPathLordYarkan;
            if (false == string.IsNullOrWhiteSpace(PathUniqueAlarmLordYarkan))
            {
                if (File.Exists(PathUniqueAlarmLordYarkan))
                {
                    _playerUniqueAlarmLordYarkan ??= new();
                    _playerUniqueAlarmLordYarkan.SoundLocation = PathUniqueAlarmLordYarkan;
                }
            }

            // Demon Shaitan
            PlayUniqueAlarmDemonShaitan = PlayerConfig.Profile.Alarms.PlayDemonShaitan;
            PathUniqueAlarmDemonShaitan = PlayerConfig.Profile.Alarms.AlarmPathDemonShaitan;
            if (false == string.IsNullOrWhiteSpace(PathUniqueAlarmDemonShaitan))
            {
                if (File.Exists(PathUniqueAlarmDemonShaitan))
                {
                    _playerUniqueAlarmDemonShaitan ??= new();
                    _playerUniqueAlarmDemonShaitan.SoundLocation = PathUniqueAlarmDemonShaitan;
                }
            }

            // General unique alarm
            PlayUniqueAlarmGeneral = PlayerConfig.Profile.Alarms.PlayGeneral;
            PathUniqueAlarmGeneral = PlayerConfig.Profile.Alarms.AlarmPathGeneral;
            if (false == string.IsNullOrWhiteSpace(PathUniqueAlarmGeneral))
            {
                if (File.Exists(PathUniqueAlarmGeneral))
                {
                    _playerUniqueAlarmGeneral ??= new();
                    _playerUniqueAlarmGeneral.SoundLocation = PathUniqueAlarmGeneral;
                }
            }

            string regTmp = PlayerConfig.Profile.Alarms.RegexGeneral;
            try
            {
                if (false == string.IsNullOrWhiteSpace(regTmp))
                {
                    RegexUniqueAlarmGeneral = new Regex(regTmp);
                }
                else
                {
                    RegexUniqueAlarmGeneral = new Regex("^.*$");
                }
            }
            catch
            {
                RegexUniqueAlarmGeneral = new Regex("^.*$");
            }
        }
    }
}
