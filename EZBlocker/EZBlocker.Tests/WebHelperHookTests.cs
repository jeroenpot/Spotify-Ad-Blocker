using System.Threading.Tasks;
using EZBlocker.Json;
using FakeItEasy;
using Newtonsoft.Json;
using NUnit.Framework;

namespace EZBlocker.Tests
{
    [TestFixture]
    public class WebHelperHookTests
    {
        [Test]
        public async Task ShouldDetectAd()
        {
            var fakeRepo = A.Fake<IWebRepository>();
            var webHelperHook = new WebHelperHook(fakeRepo);

            var data = GetResource<SpotifyAnswer>(Properties.Resources.ad);
            A.CallTo(() => fakeRepo.GetData<SpotifyAnswer>(A<string>.Ignored)).Returns(Task.FromResult(data));

            var result = await webHelperHook.GetStatus();
            Assert.That(result.IsAd, Is.True);
            Assert.That(result.DisplayLabel, Is.Null);
            Assert.That(result.IsPlaying, Is.True);
            Assert.That(result.IsRunning, Is.True);
            Assert.That(result.TimerInterval, Is.EqualTo(18821));
        }

        [Test]
        public async Task ShouldDetectNormalMusic()
        {
            var fakeRepo = A.Fake<IWebRepository>();
            var webHelperHook = new WebHelperHook(fakeRepo);

            var data = GetResource<SpotifyAnswer>(Properties.Resources.music);
            A.CallTo(() => fakeRepo.GetData<SpotifyAnswer>(A<string>.Ignored)).Returns(Task.FromResult(data));

            var result = await webHelperHook.GetStatus();
            Assert.That(result.IsAd, Is.False);
            Assert.That(result.DisplayLabel, Is.EqualTo("This World (Selah Sue)"));
            Assert.That(result.IsPlaying, Is.True);
            Assert.That(result.IsRunning, Is.True);
            Assert.That(result.TimerInterval, Is.EqualTo(157301));
        }

        private T GetResource<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
