using System.IO;
using Hangeul.App;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class AtomicFileTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hangeul_atomicfile_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Write_CreatesTheFile_WithoutLeavingATempFile()
        {
            string path = Path.Combine(_dir, "item_words.json");
            AtomicFile.Write(path, "{\"a\":1}");

            Assert.AreEqual("{\"a\":1}", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
            Assert.AreEqual(1, Directory.GetFiles(_dir).Length);
        }

        [Test]
        public void Write_ReplacesExistingContent()
        {
            string path = Path.Combine(_dir, "item_words.json");
            File.WriteAllText(path, "old old old old old");
            AtomicFile.Write(path, "new");

            Assert.AreEqual("new", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void Write_KoreanText_IsUtf8WithoutBom()
        {
            string path = Path.Combine(_dir, "item_words.json");
            AtomicFile.Write(path, "가방");

            var bytes = File.ReadAllBytes(path);
            Assert.AreEqual(new byte[] { 0xEA, 0xB0, 0x80, 0xEB, 0xB0, 0xA9 }, bytes);   // BOM(EF BB BF)이 없다
            Assert.AreEqual("가방", File.ReadAllText(path));
        }

        [Test]
        public void Write_WhenTheFolderIsMissing_ThrowsAndLeavesNothingBehind()
        {
            string path = Path.Combine(_dir, "missing", "item_words.json");
            Assert.Throws<DirectoryNotFoundException>(() => AtomicFile.Write(path, "x"));
            Assert.IsEmpty(Directory.GetFiles(_dir, "*", SearchOption.AllDirectories));
        }

        [Test]
        public void Write_WhenAStaleTempFileExists_StillWorks()
        {
            string path = Path.Combine(_dir, "item_words.json");
            File.WriteAllText(path + ".tmp", "leftover from a crash");
            File.WriteAllText(path, "old");

            AtomicFile.Write(path, "new");

            Assert.AreEqual("new", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
    }
}
