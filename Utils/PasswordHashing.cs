public static class StringUtils
{
    public delegate string HashDelegate(string password);
    public delegate bool VerifyDelegate(string password, string hashedPassword);

    public static HashDelegate overrideHash;
    public static VerifyDelegate overrideVerify;

    public static string PasswordHash(this string password)
    {
        if (overrideHash != null)
            return overrideHash.Invoke(password);
        return password.GetMD5();
    }

    public static bool PasswordVerify(this string password, string hashedPassword)
    {
        if (overrideVerify != null)
            return overrideVerify.Invoke(password, hashedPassword);
        return password.GetMD5().Equals(hashedPassword);
    }
}
