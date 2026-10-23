using System;
using System.Collections.Generic;
using FietsApp.Accounts;
using FietsApp.Training;
using NUnit.Framework;
using UnityEngine;

namespace FietsApp.Tests
{
    /// <summary>GitHub-account en profielsynchronisatie (stories 7.1 en 7.2).</summary>
    public class AccountTests
    {
        [Test]
        public void Json_ReadsGitHubAnswers()
        {
            var o = Json.Parse("{\"a\": [1, 2.5, -3e2], \"b\": {\"c.d\": \"x\\\"y\\u00e9\\n\"}, \"t\": true, \"n\": null}");
            var list = (List<object>)Json.Get(o, "a");
            CollectionAssert.AreEqual(new object[] { 1.0, 2.5, -300.0 }, list);
            Assert.AreEqual("x\"yé\n", Json.GetString(Json.Get(o, "b"), "c.d"));
            Assert.AreEqual(true, Json.Get(o, "t"));
            Assert.IsNull(Json.Get(o, "n"));
            Assert.IsNull(Json.Get(o, "ontbreekt"));
            Assert.Throws<FormatException>(() => Json.Parse("{\"a\": }"));
            Assert.Throws<FormatException>(() => Json.Parse("{} x"));
        }

        [Test]
        public void Json_QuoteRoundTrips()
        {
            const string text = "{\n  \"ftpWatts\": 250,\t\"naam\": \"Café \\ test\"\n}";
            Assert.AreEqual(text, Json.Parse(Json.Quote(text)));
        }

        [Test]
        public void DeviceFlow_CodeAndPolling()
        {
            // Voorbeeld uit de GitHub-documentatie over de device flow.
            var code = GitHub.ParseDeviceCode("{\"device_code\":\"3584d83530557fdd1f46af8289938c8ef79f9dc5\",\"user_code\":\"WDJB-MJHT\"," +
                                              "\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":900,\"interval\":5}");
            Assert.AreEqual("WDJB-MJHT", code.UserCode);
            Assert.AreEqual("https://github.com/login/device", code.VerificationUri);
            Assert.AreEqual(5, code.IntervalSeconds);
            Assert.AreEqual(900, code.ExpiresInSeconds);
            StringAssert.StartsWith("client_id=Iv1.abc&scope=gist", GitHub.DeviceCodeForm("Iv1.abc"));
            StringAssert.Contains("grant_type=urn:ietf:params:oauth:grant-type:device_code", GitHub.TokenForm("Iv1.abc", "xyz"));

            Assert.AreEqual(TokenState.Pending, GitHub.ParseToken("{\"error\":\"authorization_pending\"}").State);
            Assert.AreEqual(TokenState.SlowDown, GitHub.ParseToken("{\"error\":\"slow_down\",\"interval\":10}").State);
            Assert.AreEqual(TokenState.Expired, GitHub.ParseToken("{\"error\":\"expired_token\"}").State);
            Assert.AreEqual(TokenState.Denied, GitHub.ParseToken("{\"error\":\"access_denied\"}").State);
            var granted = GitHub.ParseToken("{\"access_token\":\"gho_16C7e42F292c6912E7710c838347Ae178B4a\",\"token_type\":\"bearer\",\"scope\":\"gist\"}");
            Assert.AreEqual(TokenState.Granted, granted.State);
            Assert.AreEqual("gho_16C7e42F292c6912E7710c838347Ae178B4a", granted.Token);
            Assert.AreEqual(TokenState.Failed, GitHub.ParseToken("<html>").State);

            var disabled = Assert.Throws<FormatException>(() => GitHub.ParseDeviceCode("{\"error\":\"device_flow_disabled\"}"));
            StringAssert.Contains("Device Flow", disabled.Message);
        }

        [Test]
        public void Gist_FindAndRead()
        {
            const string list = "[{\"id\":\"aaa\",\"files\":{\"notities.md\":{}}}," +
                                "{\"id\":\"bbb\",\"description\":\"FietsApp-profiel\",\"files\":{\"fietsapp-profiel.json\":{\"filename\":\"fietsapp-profiel.json\"}}}]";
            Assert.AreEqual("bbb", GitHub.FindProfileGist(list));
            Assert.IsNull(GitHub.FindProfileGist("[]"));

            // Wat we sturen, moet GitHub zo teruggeven; de inhoud komt er ongeschonden uit.
            string profile = "{\n    \"ftpWatts\": 250\n}";
            var created = Json.Parse(GitHub.CreateGistBody(profile));
            Assert.AreEqual(false, Json.Get(created, "public"));
            string gist = "{\"id\":\"bbb\",\"files\":{\"fietsapp-profiel.json\":{\"truncated\":false,\"content\":" + Json.Quote(profile) + "}}}";
            Assert.AreEqual(profile, GitHub.ReadProfile(gist));
            Assert.AreEqual(profile, Json.GetString(Json.Get(Json.Get(Json.Parse(GitHub.UpdateGistBody(profile)), "files"), GitHub.ProfileFile), "content"));
            Assert.IsNull(GitHub.ReadProfile("{\"files\":{}}"));
            Assert.Throws<FormatException>(() => GitHub.ReadProfile("{\"files\":{\"fietsapp-profiel.json\":{\"truncated\":true,\"content\":\"{\"}}}"));
        }

        static RiderProfile Profile(string updatedAt, int ftp, float weight) =>
            new RiderProfile { updatedAt = updatedAt, ftpWatts = ftp, weightKg = weight };

        [Test]
        public void Merge_SettingsFromTheNewest()
        {
            var laptop = Profile("2026-10-20T10:00:00Z", 250, 70f);
            var pc = Profile("2026-10-21T08:00:00Z", 260, 71f);
            pc.SetGradeScale("Mont Ventoux", 75);
            var merged = ProfileMerge.Merge(laptop, pc);
            Assert.AreEqual(260, merged.ftpWatts);
            Assert.AreEqual(71f, merged.weightKg);
            Assert.AreEqual(75, merged.GradeScale("Mont Ventoux"));
            Assert.AreEqual("2026-10-21T08:00:00Z", merged.updatedAt);
            Assert.AreEqual(250, ProfileMerge.Merge(laptop, Profile("", 200, 72f)).ftpWatts, "nooit gesynchroniseerd profiel is het oudst");
            Assert.AreSame(laptop, ProfileMerge.Merge(laptop, null));
        }

        [Test]
        public void Merge_KeepsEveryClimbTime()
        {
            var a = Profile("2026-10-21T08:00:00Z", 250, 70f);
            a.AddClimbTime("Alpe d'Huez", 3600, "2026-10-01");
            a.AddClimbTime("Alpe d'Huez", 3700, "2026-10-05");
            a.AddClimbTime("Mont Ventoux", 5400, "2026-10-03");
            var b = Profile("2026-10-20T08:00:00Z", 250, 70f);
            b.AddClimbTime("Alpe d'Huez", 3500, "2026-09-20");
            b.AddClimbTime("Alpe d'Huez", 3550, "2026-10-10");
            b.AddClimbTime("Alpe d'Huez", 3580, "2026-10-12");
            b.AddClimbTime("Stelvio", 6000, "2026-10-04");

            var merged = ProfileMerge.Merge(a, b);
            var alpe = merged.Record("Alpe d'Huez");
            Assert.AreEqual(3500, alpe.bestSeconds, "snelste van beide");
            Assert.AreEqual("2026-09-20", alpe.bestOn);
            Assert.AreEqual(3580, alpe.lastSeconds, "laatste poging van beide");
            Assert.AreEqual("2026-10-12", alpe.lastOn);
            Assert.AreEqual("2026-09-20", alpe.firstOn, "vroegste badge");
            Assert.AreEqual(3, alpe.attempts);
            Assert.IsNotNull(merged.Record("Mont Ventoux"));
            Assert.IsNotNull(merged.Record("Stelvio"));
            Assert.AreEqual(3, merged.records.Count);

            // Samenvoegen verandert de originelen niet.
            Assert.AreEqual(3600, a.Record("Alpe d'Huez").bestSeconds);
        }

        [Test]
        public void Profile_WeightAndHeartRate()
        {
            var p = new RiderProfile();
            Assert.AreEqual(80f, p.TotalMassKg, "72 kg rijder + 8 kg fiets, zoals de fysica altijd rekende");
            p.weightKg = 65f;
            Assert.AreEqual(73f, p.TotalMassKg);
            p.weightKg = 5f;
            Assert.AreEqual(80f, p.TotalMassKg, "onzinnig gewicht valt terug op de standaard");

            var old = JsonUtility.FromJson<RiderProfile>("{\"ftpWatts\": 250}");
            Assert.AreEqual(RiderProfile.DefaultWeightKg, old.weightKg);
            Assert.AreEqual(0, old.maxHeartRateBpm);
            Assert.AreEqual("", old.updatedAt);
        }
    }
}
