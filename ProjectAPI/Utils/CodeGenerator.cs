public static class CodeGenerator
{
    public static string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Random random = new Random();
        var randomCode = new string(Enumerable.Range(0, length)
                                .Select(_ => chars[random.Next(chars.Length)])
                                .ToArray());
        return randomCode;
    }
}