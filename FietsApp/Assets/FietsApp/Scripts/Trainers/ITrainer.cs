namespace FietsApp.Trainers
{
    /// <summary>
    /// Gemeenschappelijke interface voor elke vermogensbron: simulator, Bluetooth FTMS of ANT+ FE-C.
    /// Waarden zijn de laatst ontvangen meting; 0 betekent "geen data".
    /// </summary>
    public interface ITrainer
    {
        string Name { get; }
        bool IsConnected { get; }

        float PowerWatts { get; }
        float CadenceRpm { get; }
        float HeartRateBpm { get; }

        /// <summary>Hoogste helling (%) die deze trainer kan nabootsen.</summary>
        float MaxGradePercent { get; }

        /// <summary>Helling die de trainer nu nabootst, na begrenzing.</summary>
        float GradePercent { get; }

        /// <summary>Vraag de trainer om een helling na te bootsen; wordt begrensd op het bereik van de trainer.</summary>
        void SetGrade(float gradePercent);
    }
}
