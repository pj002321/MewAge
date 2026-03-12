using System;
using System.Numerics;
using UnityEngine;

public static class StringExtension
{
    public static bool IsNullOrEmpty(this string s)
    {
        return string.IsNullOrEmpty(s);
    }

    public static string Form(this string self, params object[] args)
    {
        if (self == null) return null;
        return string.Format(self, args);
    }

    public static int ParseInt(this string s)
    {
        if (IsNullOrEmpty(s))
            return 0;

#if DEBUG

        //$"Parse:{s}".DLog();
#endif

        return Int32.TryParse(s, out var result) ? result : 0;
    }

    public static long ParseLong(this string s)
    {
        if (IsNullOrEmpty(s))
            return 0L;

        return long.TryParse(s, out var result) ? result : 0;
    }

    public static BigInteger ParseBigInt(this string s)
    {
        return BigInteger.Parse(s);
    }

    public static float ParseFloat(this string s)
    {
        if (IsNullOrEmpty(s))
            return 0f;

        return float.TryParse(s, out var result) ? result : 0f;
    }

    public static double ParseDouble(this string s)
    {
        return double.Parse(s);
    }

    public static T ParseEnum<T>(this string s)
    {
        return (T)System.Enum.Parse(typeof(T), s, true);
    }

    public static T AsEnum<T>(this string s)
    {
        return ParseEnum<T>(s);
    }

    public static void Log(this String s) => Debug.Log(s);

    public static void DLog(this string s)
    {
        Debug.Log(s);
    }

    public static void DWarnning(this string s)
    {
        Debug.LogWarning(s);
    }

    public static void ELog(this string s)
    {
#if UNITY_EDITOR
        Debug.Log(s);
#endif
    }
    public static void EWarning(this string s)
    {
#if UNITY_EDITOR
        Debug.LogWarning(s);
#endif
    }
    public static void EError(this string s)
    {
#if UNITY_EDITOR
        Debug.LogError(s);
#endif
    }

    public static void DError(this string s)
    {
        Debug.LogError(s);
    }

    public static bool EqualCaseSensitive(this string a, string b)
    {
        return string.Equals(a, b, StringComparison.Ordinal);
    }

    public static bool EqualIgnoreCase(this string a, string b)
    {
        return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    public static string[] TrimAll(this string[] strs)
    {
        int len = strs.Length;
        for (int idx = 0; idx < len; idx++)
        {
            strs[idx] = strs[idx].Trim();
            //$"[TRIM ALL] [{strs[idx]}]".DLog();
        }

        return strs;
    }

    public static string SubstrInside(this string str, int step = 1)
    {
        return str.Substring(step, str.Length - (step + 1));
    }

    public static void DLog(this object obj, string message)
    {
        string className = obj.GetType().Name;
        Debug.Log($"[{className}] {message}");
    }

    public static void DWarnning(this object obj, string message)
    {
        string className = obj.GetType().Name;
        Debug.LogWarning($"[{className}] {message}");
    }


    public static void DError(this object obj, string message)
    {
        string className = obj.GetType().Name;
        Debug.LogError($"[{className}] {message}");
    }

}
