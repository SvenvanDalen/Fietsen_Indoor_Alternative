using System;

namespace FietsApp.Mobile
{
    /// <summary>
    /// Hoe groot de interface wordt (story 14.2). Alles is ontworpen op 1920×1080 en schaalt al mee met het scherm;
    /// op een klein scherm (telefoon) wordt het dan te klein om te lezen en aan te tikken. Daarom groeit elk paneel
    /// tot de interface even groot oogt als op een scherm van <see cref="TargetHeightInches"/> hoog, maar nooit verder
    /// dan het scherm toelaat.
    /// </summary>
    public static class UiScale
    {
        /// <summary>
        /// Schermhoogte (inch) waaronder de interface meegroeit: ongeveer een tablet van 10" liggend. Groter maken
        /// (zoals een laptopscherm) laat op een telefoon HUD, hoogteprofiel en knoppen over elkaar vallen.
        /// </summary>
        public const float TargetHeightInches = 4.5f;

        /// <summary>Hooguit zo veel groter, ook op een heel klein scherm.</summary>
        public const float MaxScale = 2.5f;

        /// <summary>Gewenste vergroting voor een scherm van <paramref name="heightPixels"/> hoog bij <paramref name="dpi"/>.</summary>
        public static float Desired(int heightPixels, float dpi)
        {
            if (dpi <= 0f || heightPixels <= 0) return 1f; // onbekend: zoals ontworpen
            float inches = heightPixels / dpi;
            return Math.Min(MaxScale, Math.Max(1f, TargetHeightInches / inches));
        }

        /// <summary>
        /// Vergroting voor een paneel van <paramref name="width"/>×<paramref name="height"/> op een canvas van
        /// <paramref name="canvasWidth"/>×<paramref name="canvasHeight"/> (canvaseenheden): de gewenste, of kleiner als het
        /// paneel anders buiten beeld valt. Een paneel dat ook zonder vergroting niet past, krimpt.
        /// </summary>
        public static float Fit(float desired, float width, float height, float canvasWidth, float canvasHeight, float margin = 16f)
        {
            float fitsWidth = (canvasWidth - 2f * margin) / width;
            float fitsHeight = (canvasHeight - 2f * margin) / height;
            return Math.Min(desired, Math.Min(fitsWidth, fitsHeight));
        }

        /// <summary>
        /// Ruimte (canvaseenheden) langs één as voor een paneel met draaipunt <paramref name="pivot"/> (0, 0,5 of 1)
        /// op <paramref name="position"/> van zijn anker: aan een rand vanaf die plek tot de overkant, in het midden
        /// naar beide kanten. Voor <see cref="Fit"/> met marge 0.
        /// </summary>
        public static float Available(float canvas, float pivot, float position, float margin = 16f)
        {
            bool atEdge = pivot < 0.25f || pivot > 0.75f;
            return atEdge ? canvas - Math.Abs(position) - margin : canvas - 2f * (Math.Abs(position) + margin);
        }
    }
}
