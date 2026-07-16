using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 项目自有稳定哈希入口。SHA-256 是标准定义算法，避免运行时字符串哈希实现变化。
/// </summary>
public static class StableHash
{
    public static ulong HashToUInt64(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
        byte[] digest = SHA256.HashData(bytes);
        ulong result = 0;
        for (int i = 0; i < 8; i++)
            result |= (ulong)digest[i] << (i * 8);
        return result;
    }
}
