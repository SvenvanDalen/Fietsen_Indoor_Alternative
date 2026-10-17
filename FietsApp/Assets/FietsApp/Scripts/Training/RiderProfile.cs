namespace FietsApp.Training
{
    /// <summary>
    /// Het lokale rijdersprofiel (stories 8.1 en 8.5): FTP voor zones en workouts. Velden in kleine letters, zodat
    /// <c>JsonUtility</c> het kan opslaan; accounts en een profiel in de cloud volgen bij E7.
    /// </summary>
    [System.Serializable]
    public sealed class RiderProfile
    {
        public const int DefaultFtpWatts = 200;

        /// <summary>Functional Threshold Power in watt; wordt bijgewerkt door de FTP-test (8.1).</summary>
        public int ftpWatts = DefaultFtpWatts;

        /// <summary>Datum van de laatste FTP-test (jjjj-mm-dd), leeg als de FTP nog de standaardwaarde is.</summary>
        public string ftpTestedOn = "";
    }
}
