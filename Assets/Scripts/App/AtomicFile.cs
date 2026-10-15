using System;
using System.IO;
using System.Text;

namespace Hangeul.App
{
    /// <summary>
    /// 텍스트 파일을 안전하게 쓴다: 옆의 임시 파일(<c>.tmp</c>)에 먼저 다 쓴 다음 원래 파일과 바꿔치기해서,
    /// 쓰다가 끊겨도(강제 종료, 전원, 디스크 가득 참) 기존 파일이 반쯤 쓰인 채로 남지 않게 한다.
    /// 유니티는 <c>.tmp</c> 확장자를 에셋으로 가져오지 않으므로 Assets 폴더 안에서도 쓸 수 있다.
    /// </summary>
    public static class AtomicFile
    {
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>path에 text를 쓴다(BOM 없는 UTF-8). 실패하면 예외를 던지고, 그 경우 기존 파일은 그대로다.</summary>
        public static void Write(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, Utf8NoBom);
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (IOException)
            {
                // 바꿔치기가 막히면(다른 프로그램이 파일을 잡고 있는 등) 직접 덮어쓴다 — 임시 파일에는 이미 다 쓰여 있으므로 그대로 복사한다
                try { File.Copy(tmp, path, true); }
                finally { TryDelete(tmp); }
            }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { /* 지우지 못해도 다음 저장 때 덮어쓴다 */ }
        }
    }
}
