using System;
using System.Collections.Generic;
using FietsApp.Strava;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>Strava-koppeling en automatisch uploaden (story 5.2): OAuth-berichten, uploads en de wachtrij.</summary>
    public class StravaTests
    {
        // ---- Koppelen ----

        [Test]
        public void AuthorizeUrl_AsksForActivityWriteWithLocalRedirect()
        {
            string url = StravaApi.AuthorizeUrl("12345", "http://localhost:8767/strava/", "abc");
            StringAssert.StartsWith("https://www.strava.com/oauth/authorize?", url);
            StringAssert.Contains("client_id=12345", url);
            StringAssert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A8767%2Fstrava%2F", url);
            StringAssert.Contains("response_type=code", url);
            StringAssert.Contains("scope=activity%3Awrite", url);
            StringAssert.Contains("state=abc", url);
        }

        [Test]
        public void TokenForm_HasCodeAndSecret()
        {
            Assert.AreEqual("client_id=12345&client_secret=s%26cr%3Dt&code=xyz&grant_type=authorization_code",
                StravaApi.TokenForm("12345", "s&cr=t", "xyz"));
        }

        [Test]
        public void RefreshForm_HasRefreshToken()
        {
            Assert.AreEqual("client_id=12345&client_secret=geheim&refresh_token=r1&grant_type=refresh_token",
                StravaApi.RefreshForm("12345", "geheim", "r1"));
        }

        [Test]
        public void ParseCallback_WithCodeStateAndScope_Succeeds()
        {
            var cb = StravaApi.ParseCallback("?state=abc&code=c0de&scope=read,activity:write", "abc");
            Assert.AreEqual("c0de", cb.Code);
            Assert.IsNull(cb.Error);
        }

        [Test]
        public void ParseCallback_Denied_GivesError()
        {
            var cb = StravaApi.ParseCallback("?state=abc&error=access_denied", "abc");
            Assert.IsNull(cb.Code);
            StringAssert.Contains("geweigerd", cb.Error);
        }

        [Test]
        public void ParseCallback_WrongState_GivesError()
        {
            var cb = StravaApi.ParseCallback("?state=evil&code=c0de&scope=read,activity:write", "abc");
            Assert.IsNull(cb.Code);
            Assert.IsNotNull(cb.Error);
        }

        [Test]
        public void ParseCallback_WithoutActivityWrite_GivesError()
        {
            // De rijder kan het vinkje voor uploaden weghalen op de Strava-pagina.
            var cb = StravaApi.ParseCallback("?state=abc&code=c0de&scope=read", "abc");
            Assert.IsNull(cb.Code);
            StringAssert.Contains("uploaden", cb.Error);
        }

        [Test]
        public void ParseToken_ReadsTokensExpiryAndAthlete()
        {
            var t = StravaApi.ParseToken(
                "{\"token_type\":\"Bearer\",\"expires_at\":1792000000,\"expires_in\":21600,\"refresh_token\":\"r1\"," +
                "\"access_token\":\"a1\",\"athlete\":{\"id\":42,\"firstname\":\"Jan\",\"lastname\":\"de Vries\"}}");
            Assert.AreEqual("a1", t.AccessToken);
            Assert.AreEqual("r1", t.RefreshToken);
            Assert.AreEqual(1792000000L, t.ExpiresAt);
            Assert.AreEqual("Jan de Vries", t.AthleteName);
        }

        [Test]
        public void ParseToken_RefreshWithoutAthlete_HasNoName()
        {
            var t = StravaApi.ParseToken("{\"access_token\":\"a2\",\"refresh_token\":\"r2\",\"expires_at\":1792021600}");
            Assert.AreEqual("a2", t.AccessToken);
            Assert.IsNull(t.AthleteName);
        }

        [Test]
        public void ParseToken_Error_ThrowsWithMessage()
        {
            var e = Assert.Throws<FormatException>(() => StravaApi.ParseToken(
                "{\"message\":\"Bad Request\",\"errors\":[{\"resource\":\"Application\",\"field\":\"client_secret\",\"code\":\"invalid\"}]}"));
            StringAssert.Contains("client_secret", e.Message);
        }

        [Test]
        public void NeedsRefresh_WithinFiveMinutesOfExpiry()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1792000000).UtcDateTime;
            Assert.IsTrue(StravaApi.NeedsRefresh(1792000000 + 299, now));
            Assert.IsFalse(StravaApi.NeedsRefresh(1792000000 + 301, now));
            Assert.IsTrue(StravaApi.NeedsRefresh(0, now));
        }

        // ---- Uploaden ----

        [Test]
        public void ParseUpload_StillProcessing()
        {
            var u = StravaApi.ParseUpload(
                "{\"id\":16486788,\"id_str\":\"16486788\",\"external_id\":\"rit.fit\",\"error\":null," +
                "\"status\":\"Your activity is still being processed.\",\"activity_id\":null}");
            Assert.AreEqual(UploadState.Processing, u.State);
            Assert.AreEqual(16486788L, u.UploadId);
        }

        [Test]
        public void ParseUpload_Ready_HasActivityId()
        {
            var u = StravaApi.ParseUpload(
                "{\"id\":16486788,\"error\":null,\"status\":\"Your activity is ready.\",\"activity_id\":12345678901}");
            Assert.AreEqual(UploadState.Done, u.State);
            Assert.AreEqual(12345678901L, u.ActivityId);
        }

        [Test]
        public void ParseUpload_Duplicate_CountsAsDoneWithExistingActivity()
        {
            // Staat de rit al op Strava (bijvoorbeeld met de hand geüpload), dan is hij er: niet opnieuw proberen.
            var u = StravaApi.ParseUpload(
                "{\"id\":16486788,\"error\":\"2026-10-23_1012_Mont_Ventoux.fit duplicate of activity 9876543210\"," +
                "\"status\":\"There was an error processing your activity.\",\"activity_id\":null}");
            Assert.AreEqual(UploadState.Duplicate, u.State);
            Assert.AreEqual(9876543210L, u.ActivityId);
        }

        [Test]
        public void ParseUpload_Error_Failed()
        {
            var u = StravaApi.ParseUpload(
                "{\"id\":16486788,\"error\":\"Error parsing file.\",\"status\":\"There was an error processing your activity.\",\"activity_id\":null}");
            Assert.AreEqual(UploadState.Failed, u.State);
            Assert.AreEqual("Error parsing file.", u.Error);
        }

        [Test]
        public void ActivityUrl_PointsToStrava() =>
            Assert.AreEqual("https://www.strava.com/activities/12345678901", StravaApi.ActivityUrl(12345678901));

        [Test]
        public void RideFileName_ReadsDateAndClimb()
        {
            Assert.IsTrue(RideFileName.TryParse("2026-10-23_1012_Mont_Ventoux.fit", out var local, out var climb));
            Assert.AreEqual(new DateTime(2026, 10, 23, 10, 12, 0), local);
            Assert.AreEqual("Mont Ventoux", climb);
            Assert.AreEqual("Mont Ventoux (FietsApp)", RideFileName.ActivityName("2026-10-23_1012_Mont_Ventoux.fit"));
        }

        [Test]
        public void RideFileName_OtherName_UsesWholeName()
        {
            Assert.IsFalse(RideFileName.TryParse("mijn rit.fit", out _, out var climb));
            Assert.AreEqual("mijn rit", climb);
            Assert.AreEqual("mijn rit (FietsApp)", RideFileName.ActivityName("mijn rit.fit"));
        }

        // ---- Wachtrij ----

        static readonly DateTime Connected = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        static RideFile Ride(string name, int minutesAfterConnect) => new RideFile(name, Connected.AddMinutes(minutesAfterConnect));

        [Test]
        public void Pending_OnlyRidesSinceConnecting_OldestFirst()
        {
            var files = new[] { Ride("c.fit", 90), Ride("oud.fit", -60), Ride("a.fit", 10) };
            CollectionAssert.AreEqual(new[] { "a.fit", "c.fit" }, UploadQueue.Pending(files, Connected, new HashSet<string>()));
        }

        [Test]
        public void Pending_SkipsHandledRides()
        {
            var files = new[] { Ride("a.fit", 10), Ride("b.fit", 20) };
            CollectionAssert.AreEqual(new[] { "b.fit" }, UploadQueue.Pending(files, Connected, new HashSet<string> { "a.fit" }));
        }

        [Test]
        public void Older_CountsRidesFromBeforeConnecting()
        {
            var files = new[] { Ride("oud1.fit", -60), Ride("oud2.fit", -30), Ride("nieuw.fit", 5) };
            CollectionAssert.AreEqual(new[] { "oud1.fit", "oud2.fit" },
                UploadQueue.Older(files, Connected, new HashSet<string>()));
            CollectionAssert.AreEqual(new[] { "oud2.fit" },
                UploadQueue.Older(files, Connected, new HashSet<string> { "oud1.fit" }));
        }

        [Test]
        public void Pending_NotConnected_Nothing()
        {
            CollectionAssert.IsEmpty(UploadQueue.Pending(new[] { Ride("a.fit", 10) }, null, new HashSet<string>()));
        }
    }
}
