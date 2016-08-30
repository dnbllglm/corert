// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text;

namespace System
{
    public partial class String
    {
        private const int TrimHead = 0;
        private const int TrimTail = 1;
        private const int TrimBoth = 2;

        unsafe private static void FillStringChecked(String dest, int destPos, String src)
        {
            if (src.Length > dest.Length - destPos)
            {
                throw new IndexOutOfRangeException();
            }

            fixed (char* pDest = &dest._firstChar)
                fixed (char* pSrc = &src._firstChar)
            {
                wstrcpy(pDest + destPos, pSrc, src.Length);
            }
        }

        public static String Concat(Object arg0)
        {
            if (arg0 == null)
            {
                return String.Empty;
            }
            return arg0.ToString();
        }

        public static String Concat(Object arg0, Object arg1)
        {
            if (arg0 == null)
            {
                arg0 = String.Empty;
            }

            if (arg1 == null)
            {
                arg1 = String.Empty;
            }
            return Concat(arg0.ToString(), arg1.ToString());
        }

        public static String Concat(Object arg0, Object arg1, Object arg2)
        {
            if (arg0 == null)
            {
                arg0 = String.Empty;
            }

            if (arg1 == null)
            {
                arg1 = String.Empty;
            }

            if (arg2 == null)
            {
                arg2 = String.Empty;
            }

            return Concat(arg0.ToString(), arg1.ToString(), arg2.ToString());
        }

        public static string Concat(params object[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            if (args.Length <= 1)
            {
                return args.Length == 0 ?
                    string.Empty :
                    args[0]?.ToString() ?? string.Empty;
            }

            // We need to get an intermediary string array
            // to fill with each of the args' ToString(),
            // and then just concat that in one operation.

            // This way we avoid any intermediary string representations,
            // or buffer resizing if we use StringBuilder (although the
            // latter case is partially alleviated due to StringBuilder's
            // linked-list style implementation)

            var strings = new string[args.Length];

            int totalLength = 0;

            for (int i = 0; i < args.Length; i++)
            {
                object value = args[i];

                string toString = value?.ToString() ?? string.Empty; // We need to handle both the cases when value or value.ToString() is null
                strings[i] = toString;

                totalLength += toString.Length;

                if (totalLength < 0) // Check for a positive overflow
                {
                    throw new OutOfMemoryException();
                }
            }

            // If all of the ToStrings are null/empty, just return string.Empty
            if (totalLength == 0)
            {
                return string.Empty;
            }

            string result = FastAllocateString(totalLength);
            int position = 0; // How many characters we've copied so far

            for (int i = 0; i < strings.Length; i++)
            {
                string s = strings[i];

                Contract.Assert(s != null);
                Contract.Assert(position <= totalLength - s.Length, "We didn't allocate enough space for the result string!");

                FillStringChecked(result, position, s);
                position += s.Length;
            }

            return result;
        }

        public static string Concat<T>(IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (IEnumerator<T> en = values.GetEnumerator())
            {
                if (!en.MoveNext())
                    return string.Empty;

                // We called MoveNext once, so this will be the first item
                T currentValue = en.Current;

                // Call ToString before calling MoveNext again, since
                // we want to stay consistent with the below loop
                // Everything should be called in the order
                // MoveNext-Current-ToString, unless further optimizations
                // can be made, to avoid breaking changes
                string firstString = currentValue?.ToString();

                // If there's only 1 item, simply call ToString on that
                if (!en.MoveNext())
                {
                    // We have to handle the case of either currentValue
                    // or its ToString being null
                    return firstString ?? string.Empty;
                }

                StringBuilder result = StringBuilderCache.Acquire();

                result.Append(firstString);

                do
                {
                    currentValue = en.Current;

                    if (currentValue != null)
                    {
                        result.Append(currentValue.ToString());
                    }
                }
                while (en.MoveNext());

                return StringBuilderCache.GetStringAndRelease(result);
            }
        }

        public static string Concat(IEnumerable<string> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (IEnumerator<string> en = values.GetEnumerator())
            {
                if (!en.MoveNext())
                    return string.Empty;

                string firstValue = en.Current;

                if (!en.MoveNext())
                {
                    return firstValue ?? string.Empty;
                }

                StringBuilder result = StringBuilderCache.Acquire();
                result.Append(firstValue);

                do
                {
                    result.Append(en.Current);
                }
                while (en.MoveNext());

                return StringBuilderCache.GetStringAndRelease(result);
            }
        }

        public static String Concat(String str0, String str1)
        {
            if (IsNullOrEmpty(str0))
            {
                if (IsNullOrEmpty(str1))
                {
                    return String.Empty;
                }
                return str1;
            }

            if (IsNullOrEmpty(str1))
            {
                return str0;
            }

            int str0Length = str0.Length;

            String result = FastAllocateString(str0Length + str1.Length);

            FillStringChecked(result, 0, str0);
            FillStringChecked(result, str0Length, str1);

            return result;
        }

        public static String Concat(String str0, String str1, String str2)
        {
            if (IsNullOrEmpty(str0))
            {
                return Concat(str1, str2);
            }

            if (IsNullOrEmpty(str1))
            {
                return Concat(str0, str2);
            }

            if (IsNullOrEmpty(str2))
            {
                return Concat(str0, str1);
            }

            int totalLength = str0.Length + str1.Length + str2.Length;

            String result = FastAllocateString(totalLength);
            FillStringChecked(result, 0, str0);
            FillStringChecked(result, str0.Length, str1);
            FillStringChecked(result, str0.Length + str1.Length, str2);

            return result;
        }

        public static String Concat(String str0, String str1, String str2, String str3)
        {
            if (IsNullOrEmpty(str0))
            {
                return Concat(str1, str2, str3);
            }

            if (IsNullOrEmpty(str1))
            {
                return Concat(str0, str2, str3);
            }

            if (IsNullOrEmpty(str2))
            {
                return Concat(str0, str1, str3);
            }

            if (IsNullOrEmpty(str3))
            {
                return Concat(str0, str1, str2);
            }

            int totalLength = str0.Length + str1.Length + str2.Length + str3.Length;

            String result = FastAllocateString(totalLength);
            FillStringChecked(result, 0, str0);
            FillStringChecked(result, str0.Length, str1);
            FillStringChecked(result, str0.Length + str1.Length, str2);
            FillStringChecked(result, str0.Length + str1.Length + str2.Length, str3);

            return result;
        }

        public static String Concat(params String[] values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (values.Length <= 1)
            {
                return values.Length == 0 ?
                    string.Empty :
                    values[0] ?? string.Empty;
            }

            // It's possible that the input values array could be changed concurrently on another
            // thread, such that we can't trust that each read of values[i] will be equivalent.
            // Worst case, we can make a defensive copy of the array and use that, but we first
            // optimistically try the allocation and copies assuming that the array isn't changing,
            // which represents the 99.999% case, in particular since string.Concat is used for
            // string concatenation by the languages, with the input array being a params array.

            // Sum the lengths of all input strings
            long totalLengthLong = 0;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (value != null)
                {
                    totalLengthLong += value.Length;
                }
            }

            // If it's too long, fail, or if it's empty, return an empty string.
            if (totalLengthLong > int.MaxValue)
            {
                throw new OutOfMemoryException();
            }
            int totalLength = (int)totalLengthLong;
            if (totalLength == 0)
            {
                return string.Empty;
            }

            // Allocate a new string and copy each input string into it
            string result = FastAllocateString(totalLength);
            int copiedLength = 0;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (!string.IsNullOrEmpty(value))
                {
                    int valueLen = value.Length;
                    if (valueLen > totalLength - copiedLength)
                    {
                        copiedLength = -1;
                        break;
                    }

                    FillStringChecked(result, copiedLength, value);
                    copiedLength += valueLen;
                }
            }

            // If we copied exactly the right amount, return the new string.  Otherwise,
            // something changed concurrently to mutate the input array: fall back to
            // doing the concatenation again, but this time with a defensive copy. This
            // fall back should be extremely rare.
            return copiedLength == totalLength ? result : Concat((string[])values.Clone());
        }

        public static String Format(String format, params Object[] args)
        {
            if (args == null)
            {
                // To preserve the original exception behavior, throw an exception about format if both
                // args and format are null. The actual null check for format is in FormatHelper.
                throw new ArgumentNullException((format == null) ? "format" : "args");
            }

            return FormatHelper(null, format, new ParamsArray(args));
        }

        public static String Format(String format, Object arg0)
        {
            return FormatHelper(null, format, new ParamsArray(arg0));
        }

        public static String Format(String format, Object arg0, Object arg1)
        {
            return FormatHelper(null, format, new ParamsArray(arg0, arg1));
        }

        public static String Format(String format, Object arg0, Object arg1, Object arg2)
        {
            return FormatHelper(null, format, new ParamsArray(arg0, arg1, arg2));
        }

        public static String Format(IFormatProvider provider, String format, params Object[] args)
        {
            if (args == null)
            {
                // To preserve the original exception behavior, throw an exception about format if both
                // args and format are null. The actual null check for format is in FormatHelper.
                throw new ArgumentNullException((format == null) ? "format" : "args");
            }

            return FormatHelper(provider, format, new ParamsArray(args));
        }

        public static String Format(IFormatProvider provider, String format, Object arg0)
        {
            return FormatHelper(provider, format, new ParamsArray(arg0));
        }

        public static String Format(IFormatProvider provider, String format, Object arg0, Object arg1)
        {
            return FormatHelper(provider, format, new ParamsArray(arg0, arg1));
        }

        public static String Format(IFormatProvider provider, String format, Object arg0, Object arg1, Object arg2)
        {
            return FormatHelper(provider, format, new ParamsArray(arg0, arg1, arg2));
        }

        private static String FormatHelper(IFormatProvider provider, String format, ParamsArray args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            return StringBuilderCache.GetStringAndRelease(
                StringBuilderCache
                    .Acquire(format.Length + args.Length * 8)
                    .AppendFormatHelper(provider, format, args));
        }

        public String Insert(int startIndex, String value)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            if (startIndex < 0 || startIndex > this.Length)
                throw new ArgumentOutOfRangeException("startIndex");

            int oldLength = Length;
            int insertLength = value.Length;

            if (oldLength == 0)
                return value;
            if (insertLength == 0)
                return this;

            int newLength = oldLength + insertLength;
            if (newLength < 0)
                throw new OutOfMemoryException();
            String result = FastAllocateString(newLength);
            unsafe
            {
                fixed (char* srcThis = &_firstChar)
                {
                    fixed (char* srcInsert = &value._firstChar)
                    {
                        fixed (char* dst = &result._firstChar)
                        {
                            wstrcpy(dst, srcThis, startIndex);
                            wstrcpy(dst + startIndex, srcInsert, insertLength);
                            wstrcpy(dst + startIndex + insertLength, srcThis + startIndex, oldLength - startIndex);
                        }
                    }
                }
            }
            return result;
        }

        // Joins an array of strings together as one string with a separator between each original string.
        //
        public static String Join(String separator, params String[] value)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            return Join(separator, value, 0, value.Length);
        }

        public static string Join(string separator, params object[] values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (values.Length == 0 || values[0] == null)
                return string.Empty;

            string firstString = values[0].ToString();

            if (values.Length == 1)
            {
                return firstString ?? string.Empty;
            }

            StringBuilder result = StringBuilderCache.Acquire();
            result.Append(firstString);

            for (int i = 1; i < values.Length; i++)
            {
                result.Append(separator);
                object value = values[i];
                if (value != null)
                {
                    result.Append(value.ToString());
                }
            }

            return StringBuilderCache.GetStringAndRelease(result);
        }

        public static String Join<T>(String separator, IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (IEnumerator<T> en = values.GetEnumerator())
            {
                if (!en.MoveNext())
                    return string.Empty;

                // We called MoveNext once, so this will be the first item
                T currentValue = en.Current;

                // Call ToString before calling MoveNext again, since
                // we want to stay consistent with the below loop
                // Everything should be called in the order
                // MoveNext-Current-ToString, unless further optimizations
                // can be made, to avoid breaking changes
                string firstString = currentValue?.ToString();

                // If there's only 1 item, simply call ToString on that
                if (!en.MoveNext())
                {
                    // We have to handle the case of either currentValue
                    // or its ToString being null
                    return firstString ?? string.Empty;
                }

                StringBuilder result = StringBuilderCache.Acquire();

                result.Append(firstString);

                do
                {
                    currentValue = en.Current;

                    result.Append(separator);
                    if (currentValue != null)
                    {
                        result.Append(currentValue.ToString());
                    }
                }
                while (en.MoveNext());

                return StringBuilderCache.GetStringAndRelease(result);
            }
        }

        public static String Join(String separator, IEnumerable<String> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (IEnumerator<String> en = values.GetEnumerator())
            {
                if (!en.MoveNext())
                    return String.Empty;

                String firstValue = en.Current;

                if (!en.MoveNext())
                {
                    // Only one value available
                    return firstValue ?? String.Empty;
                }

                // Null separator and values are handled by the StringBuilder
                StringBuilder result = StringBuilderCache.Acquire();
                result.Append(firstValue);

                do
                {
                    result.Append(separator);
                    result.Append(en.Current);
                } while (en.MoveNext());
                return StringBuilderCache.GetStringAndRelease(result);
            }
        }

        // Joins an array of strings together as one string with a separator between each original string.
        //
        public unsafe static String Join(String separator, String[] value, int startIndex, int count)
        {
            //Range check the array
            if (value == null)
                throw new ArgumentNullException("value");

            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndex);
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NegativeCount);

            if (startIndex > value.Length - count)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_IndexCountBuffer);

            //Treat null as empty string.
            if (separator == null)
            {
                separator = String.Empty;
            }

            //If count is 0, that skews a whole bunch of the calculations below, so just special case that.
            if (count == 0)
            {
                return String.Empty;
            }

            if (count == 1)
            {
                return value[startIndex] ?? String.Empty;
            }

            int endIndex = startIndex + count - 1;
            StringBuilder result = StringBuilderCache.Acquire();
            // Append the first string first and then append each following string prefixed by the separator.
            result.Append(value[startIndex]);
            for (int stringToJoinIndex = startIndex + 1; stringToJoinIndex <= endIndex; stringToJoinIndex++)
            {
                result.Append(separator);
                result.Append(value[stringToJoinIndex]);
            }

            return StringBuilderCache.GetStringAndRelease(result);
        }

        public String PadLeft(int totalWidth)
        {
            return PadLeft(totalWidth, ' ');
        }

        public String PadLeft(int totalWidth, char paddingChar)
        {
            if (totalWidth < 0)
                throw new ArgumentOutOfRangeException("totalWidth", SR.ArgumentOutOfRange_NeedNonNegNum);
            int oldLength = Length;
            int count = totalWidth - oldLength;
            if (count <= 0)
                return this;
            String result = FastAllocateString(totalWidth);
            unsafe
            {
                fixed (char* dst = &result._firstChar)
                {
                    for (int i = 0; i < count; i++)
                        dst[i] = paddingChar;
                    fixed (char* src = &_firstChar)
                    {
                        wstrcpy(dst + count, src, oldLength);
                    }
                }
            }
            return result;
        }

        public String PadRight(int totalWidth)
        {
            return PadRight(totalWidth, ' ');
        }

        public String PadRight(int totalWidth, char paddingChar)
        {
            if (totalWidth < 0)
                throw new ArgumentOutOfRangeException("totalWidth", SR.ArgumentOutOfRange_NeedNonNegNum);
            int oldLength = Length;
            int count = totalWidth - oldLength;
            if (count <= 0)
                return this;
            String result = FastAllocateString(totalWidth);
            unsafe
            {
                fixed (char* dst = &result._firstChar)
                {
                    fixed (char* src = &_firstChar)
                    {
                        wstrcpy(dst, src, oldLength);
                    }
                    for (int i = 0; i < count; i++)
                        dst[oldLength + i] = paddingChar;
                }
            }
            return result;
        }

        public String Remove(int startIndex, int count)
        {
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndex);
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NegativeCount);
            int oldLength = this.Length;
            if (count > oldLength - startIndex)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_IndexCount);

            if (count == 0)
                return this;
            int newLength = oldLength - count;
            if (newLength == 0)
                return string.Empty;

            String result = FastAllocateString(newLength);
            unsafe
            {
                fixed (char* src = &_firstChar)
                {
                    fixed (char* dst = &result._firstChar)
                    {
                        wstrcpy(dst, src, startIndex);
                        wstrcpy(dst + startIndex, src + startIndex + count, newLength - startIndex);
                    }
                }
            }
            return result;
        }

        // a remove that just takes a startindex. 
        public string Remove(int startIndex)
        {
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex",
                        SR.ArgumentOutOfRange_StartIndex);
            }

            if (startIndex >= Length)
            {
                throw new ArgumentOutOfRangeException("startIndex",
                        SR.ArgumentOutOfRange_StartIndexLessThanLength);
            }

            return Substring(0, startIndex);
        }

        // Replaces all instances of oldChar with newChar.
        //
        public String Replace(char oldChar, char newChar)
        {
            if (oldChar == newChar)
                return this;

            unsafe
            {
                int remainingLength = Length;

                fixed (char* pChars = &_firstChar)
                {
                    char* pSrc = pChars;

                    while (remainingLength > 0)
                    {
                        if (*pSrc == oldChar)
                        {
                            break;
                        }

                        remainingLength--;
                        pSrc++;
                    }
                }

                if (remainingLength == 0)
                    return this;

                String result = FastAllocateString(Length);

                fixed (char* pChars = &_firstChar)
                {
                    fixed (char* pResult = &result._firstChar)
                    {
                        int copyLength = Length - remainingLength;

                        //Copy the characters already proven not to match.
                        if (copyLength > 0)
                        {
                            wstrcpy(pResult, pChars, copyLength);
                        }

                        //Copy the remaining characters, doing the replacement as we go.
                        char* pSrc = pChars + copyLength;
                        char* pDst = pResult + copyLength;

                        do
                        {
                            char currentChar = *pSrc;
                            if (currentChar == oldChar)
                                currentChar = newChar;
                            *pDst = currentChar;

                            remainingLength--;
                            pSrc++;
                            pDst++;
                        } while (remainingLength > 0);
                    }
                }

                return result;
            }
        }

        public String Replace(String oldValue, String newValue)
        {
            unsafe
            {
                if (oldValue == null)
                    throw new ArgumentNullException("oldValue");
                if (oldValue.Length == 0)
                    throw new ArgumentException(SR.Format(SR.Argument_StringZeroLength, "oldValue"));
                // Api behavior: if newValue is null, instances of oldValue are to be removed.
                if (newValue == null)
                    newValue = String.Empty;

                int numOccurrences = 0;
                int[] replacementIndices = new int[this.Length];
                fixed (char* pThis = &_firstChar)
                {
                    fixed (char* pOldValue = &oldValue._firstChar)
                    {
                        int idx = 0;
                        int lastPossibleMatchIdx = this.Length - oldValue.Length;
                        while (idx <= lastPossibleMatchIdx)
                        {
                            int probeIdx = idx;
                            int oldValueIdx = 0;
                            bool foundMismatch = false;
                            while (oldValueIdx < oldValue.Length)
                            {
                                Debug.Assert(probeIdx >= 0 && probeIdx < this.Length);
                                Debug.Assert(oldValueIdx >= 0 && oldValueIdx < oldValue.Length);
                                if (pThis[probeIdx] != pOldValue[oldValueIdx])
                                {
                                    foundMismatch = true;
                                    break;
                                }
                                probeIdx++;
                                oldValueIdx++;
                            }
                            if (!foundMismatch)
                            {
                                // Found a match for the string. Record the location of the match and skip over the "oldValue."
                                replacementIndices[numOccurrences++] = idx;
                                Debug.Assert(probeIdx == idx + oldValue.Length);
                                idx = probeIdx;
                            }
                            else
                            {
                                idx++;
                            }
                        }
                    }
                }

                if (numOccurrences == 0)
                    return this;

                int dstLength = checked(this.Length + (newValue.Length - oldValue.Length) * numOccurrences);
                String dst = FastAllocateString(dstLength);
                fixed (char* pThis = &_firstChar)
                {
                    fixed (char* pDst = &dst._firstChar)
                    {
                        fixed (char* pNewValue = &newValue._firstChar)
                        {
                            int dstIdx = 0;
                            int thisIdx = 0;

                            for (int r = 0; r < numOccurrences; r++)
                            {
                                int replacementIdx = replacementIndices[r];

                                // Copy over the non-matching portion of the original that precedes this occurrence of oldValue.
                                int count = replacementIdx - thisIdx;
                                Debug.Assert(count >= 0);
                                Debug.Assert(thisIdx >= 0 && thisIdx <= this.Length - count);
                                Debug.Assert(dstIdx >= 0 && dstIdx <= dst.Length - count);
                                if (count != 0)
                                {
                                    wstrcpy(&(pDst[dstIdx]), &(pThis[thisIdx]), count);
                                    dstIdx += count;
                                }
                                thisIdx = replacementIdx + oldValue.Length;

                                // Copy over newValue to replace the oldValue.
                                Debug.Assert(thisIdx >= 0 && thisIdx <= this.Length);
                                Debug.Assert(dstIdx >= 0 && dstIdx <= dst.Length - newValue.Length);
                                wstrcpy(&(pDst[dstIdx]), pNewValue, newValue.Length);
                                dstIdx += newValue.Length;
                            }

                            // Copy over the final non-matching portion at the end of the string.
                            int tailLength = this.Length - thisIdx;
                            Debug.Assert(tailLength >= 0);
                            Debug.Assert(thisIdx == this.Length - tailLength);
                            Debug.Assert(dstIdx == dst.Length - tailLength);
                            wstrcpy(&(pDst[dstIdx]), &(pThis[thisIdx]), tailLength);
                        }
                    }
                }

                return dst;
            }
        }

        // Creates an array of strings by splitting this string at each
        // occurrence of a separator.  The separator is searched for, and if found,
        // the substring preceding the occurrence is stored as the first element in
        // the array of strings.  We then continue in this manner by searching
        // the substring that follows the occurrence.  On the other hand, if the separator
        // is not found, the array of strings will contain this instance as its only element.
        // If the separator is null
        // whitespace (i.e., Character.IsWhitespace) is used as the separator.
        //
        public String[] Split(params char[] separator)
        {
            return Split(separator, Int32.MaxValue, StringSplitOptions.None);
        }

        // Creates an array of strings by splitting this string at each
        // occurrence of a separator.  The separator is searched for, and if found,
        // the substring preceding the occurrence is stored as the first element in
        // the array of strings.  We then continue in this manner by searching
        // the substring that follows the occurrence.  On the other hand, if the separator
        // is not found, the array of strings will contain this instance as its only element.
        // If the separator is the empty string (i.e., String.Empty), then
        // whitespace (i.e., Character.IsWhitespace) is used as the separator.
        // If there are more than count different strings, the last n-(count-1)
        // elements are concatenated and added as the last String.
        //
        public string[] Split(char[] separator, int count)
        {
            return Split(separator, count, StringSplitOptions.None);
        }

        public String[] Split(char[] separator, StringSplitOptions options)
        {
            return Split(separator, Int32.MaxValue, options);
        }

        public String[] Split(char[] separator, int count, StringSplitOptions options)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count",
                    SR.ArgumentOutOfRange_NegativeCount);

            if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries)
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, options));

            bool omitEmptyEntries = (options == StringSplitOptions.RemoveEmptyEntries);

            if ((count == 0) || (omitEmptyEntries && this.Length == 0))
            {
                return Array.Empty<String>();
            }

            if (count == 1)
            {
                return new String[] { this };
            }

            int[] sepList = new int[Length];
            int numReplaces = MakeSeparatorList(separator, sepList);

            // Handle the special case of no replaces.
            if (0 == numReplaces)
            {
                return new String[] { this };
            }

            if (omitEmptyEntries)
            {
                return SplitOmitEmptyEntries(sepList, null, numReplaces, count);
            }
            else
            {
                return SplitKeepEmptyEntries(sepList, null, numReplaces, count);
            }
        }

        public String[] Split(String[] separator, StringSplitOptions options)
        {
            return Split(separator, Int32.MaxValue, options);
        }

        public String[] Split(String[] separator, Int32 count, StringSplitOptions options)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count",
                    SR.ArgumentOutOfRange_NegativeCount);
            }

            if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries)
            {
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, (int)options));
            }

            bool omitEmptyEntries = (options == StringSplitOptions.RemoveEmptyEntries);

            if (separator == null || separator.Length == 0)
            {
                return Split((char[])null, count, options);
            }

            if ((count == 0) || (omitEmptyEntries && this.Length == 0))
            {
                return Array.Empty<String>();
            }

            if (count == 1)
            {
                return new String[] { this };
            }

            int[] sepList = new int[Length];
            int[] lengthList = new int[Length];
            int numReplaces = MakeSeparatorList(separator, sepList, lengthList);

            //Handle the special case of no replaces.
            if (0 == numReplaces)
            {
                return new String[] { this };
            }

            if (omitEmptyEntries)
            {
                return SplitOmitEmptyEntries(sepList, lengthList, numReplaces, count);
            }
            else
            {
                return SplitKeepEmptyEntries(sepList, lengthList, numReplaces, count);
            }
        }

        // Note a special case in this function:
        //     If there is no separator in the string, a string array which only contains 
        //     the original string will be returned regardless of the count. 
        //

        private String[] SplitKeepEmptyEntries(Int32[] sepList, Int32[] lengthList, Int32 numReplaces, int count)
        {
            int currIndex = 0;
            int arrIndex = 0;

            count--;
            int numActualReplaces = (numReplaces < count) ? numReplaces : count;

            //Allocate space for the new array.
            //+1 for the string from the end of the last replace to the end of the String.
            String[] splitStrings = new String[numActualReplaces + 1];

            for (int i = 0; i < numActualReplaces && currIndex < Length; i++)
            {
                splitStrings[arrIndex++] = Substring(currIndex, sepList[i] - currIndex);
                currIndex = sepList[i] + ((lengthList == null) ? 1 : lengthList[i]);
            }

            //Handle the last string at the end of the array if there is one.
            if (currIndex < Length && numActualReplaces >= 0)
            {
                splitStrings[arrIndex] = Substring(currIndex);
            }
            else if (arrIndex == numActualReplaces)
            {
                //We had a separator character at the end of a string.  Rather than just allowing
                //a null character, we'll replace the last element in the array with an empty string.
                splitStrings[arrIndex] = String.Empty;
            }

            return splitStrings;
        }


        // This function will not keep the Empty String 
        private String[] SplitOmitEmptyEntries(Int32[] sepList, Int32[] lengthList, Int32 numReplaces, int count)
        {
            // Allocate array to hold items. This array may not be 
            // filled completely in this function, we will create a 
            // new array and copy string references to that new array.

            int maxItems = (numReplaces < count) ? (numReplaces + 1) : count;
            String[] splitStrings = new String[maxItems];

            int currIndex = 0;
            int arrIndex = 0;

            for (int i = 0; i < numReplaces && currIndex < Length; i++)
            {
                if (sepList[i] - currIndex > 0)
                {
                    splitStrings[arrIndex++] = Substring(currIndex, sepList[i] - currIndex);
                }
                currIndex = sepList[i] + ((lengthList == null) ? 1 : lengthList[i]);
                if (arrIndex == count - 1)
                {
                    // If all the remaining entries at the end are empty, skip them
                    while (i < numReplaces - 1 && currIndex == sepList[++i])
                    {
                        currIndex += ((lengthList == null) ? 1 : lengthList[i]);
                    }
                    break;
                }
            }

            //Handle the last string at the end of the array if there is one.
            if (currIndex < Length)
            {
                splitStrings[arrIndex++] = Substring(currIndex);
            }

            String[] stringArray = splitStrings;
            if (arrIndex != maxItems)
            {
                stringArray = new String[arrIndex];
                for (int j = 0; j < arrIndex; j++)
                {
                    stringArray[j] = splitStrings[j];
                }
            }
            return stringArray;
        }

        //--------------------------------------------------------------------    
        // This function returns the number of the places within this instance where 
        // characters in Separator occur.
        // Args: separator  -- A string containing all of the split characters.
        //       sepList    -- an array of ints for split char indicies.
        //--------------------------------------------------------------------    
        private unsafe int MakeSeparatorList(char[] separator, int[] sepList)
        {
            int foundCount = 0;

            if (separator == null || separator.Length == 0)
            {
                fixed (char* pwzChars = &_firstChar)
                {
                    //If they passed null or an empty string, look for whitespace.
                    for (int i = 0; i < Length && foundCount < sepList.Length; i++)
                    {
                        if (Char.IsWhiteSpace(pwzChars[i]))
                        {
                            sepList[foundCount++] = i;
                        }
                    }
                }
            }
            else
            {
                int sepListCount = sepList.Length;
                int sepCount = separator.Length;
                //If they passed in a string of chars, actually look for those chars.
                fixed (char* pwzChars = &_firstChar, pSepChars = separator)
                {
                    for (int i = 0; i < Length && foundCount < sepListCount; i++)
                    {
                        char* pSep = pSepChars;
                        for (int j = 0; j < sepCount; j++, pSep++)
                        {
                            if (pwzChars[i] == *pSep)
                            {
                                sepList[foundCount++] = i;
                                break;
                            }
                        }
                    }
                }
            }
            return foundCount;
        }

        //--------------------------------------------------------------------    
        // This function returns the number of the places within this instance where 
        // instances of separator strings occur.
        // Args: separators -- An array containing all of the split strings.
        //       sepList    -- an array of ints for split string indicies.
        //       lengthList -- an array of ints for split string lengths.
        //--------------------------------------------------------------------    
        private unsafe int MakeSeparatorList(String[] separators, int[] sepList, int[] lengthList)
        {
            int foundCount = 0;
            int sepListCount = sepList.Length;
            int sepCount = separators.Length;

            fixed (char* pwzChars = &_firstChar)
            {
                for (int i = 0; i < Length && foundCount < sepListCount; i++)
                {
                    for (int j = 0; j < separators.Length; j++)
                    {
                        String separator = separators[j];
                        if (String.IsNullOrEmpty(separator))
                        {
                            continue;
                        }
                        Int32 currentSepLength = separator.Length;
                        if (pwzChars[i] == separator[0] && currentSepLength <= Length - i)
                        {
                            if (currentSepLength == 1
                                || String.CompareOrdinal(this, i, separator, 0, currentSepLength) == 0)
                            {
                                sepList[foundCount] = i;
                                lengthList[foundCount] = currentSepLength;
                                foundCount++;
                                i += currentSepLength - 1;
                                break;
                            }
                        }
                    }
                }
            }
            return foundCount;
        }

        // Returns a substring of this string.
        //
        public String Substring(int startIndex)
        {
            return this.Substring(startIndex, Length - startIndex);
        }

        // Returns a substring of this string.
        //
        public String Substring(int startIndex, int length)
        {
            //Bounds Checking.
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndex);
            }

            if (startIndex > Length)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_StartIndexLargerThanLength);
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_NegativeLength);
            }

            if (startIndex > Length - length)
            {
                throw new ArgumentOutOfRangeException("length", SR.ArgumentOutOfRange_IndexLength);
            }

            if (length == 0)
            {
                return String.Empty;
            }

            if (startIndex == 0 && length == this.Length)
            {
                return this;
            }

            return InternalSubString(startIndex, length);
        }

        private unsafe string InternalSubString(int startIndex, int length)
        {
            String result = FastAllocateString(length);

            fixed (char* dest = &result._firstChar)
                fixed (char* src = &_firstChar)
            {
                wstrcpy(dest, src + startIndex, length);
            }

            return result;
        }

        // Creates a copy of this string in lower case.  The culture is set by culture.
        public String ToLower()
        {
            return FormatProvider.ToLower(this);
        }

        // Creates a copy of this string in lower case based on invariant culture.
        public String ToLowerInvariant()
        {
            return FormatProvider.ToLowerInvariant(this);
        }

        public String ToUpper()
        {
            return FormatProvider.ToUpper(this);
        }

        //Creates a copy of this string in upper case based on invariant culture.
        public String ToUpperInvariant()
        {
            return FormatProvider.ToUpperInvariant(this);
        }

        // Removes a set of characters from the end of this string.

        public String Trim(params char[] trimChars)
        {
            if (null == trimChars || trimChars.Length == 0)
            {
                return TrimHelper(TrimBoth);
            }
            return TrimHelper(trimChars, TrimBoth);
        }

        // Removes a set of characters from the beginning of this string.
        public String TrimStart(params char[] trimChars)
        {
            if (null == trimChars || trimChars.Length == 0)
            {
                return TrimHelper(TrimHead);
            }
            return TrimHelper(trimChars, TrimHead);
        }


        // Removes a set of characters from the end of this string.
        public String TrimEnd(params char[] trimChars)
        {
            if (null == trimChars || trimChars.Length == 0)
            {
                return TrimHelper(TrimTail);
            }
            return TrimHelper(trimChars, TrimTail);
        }

        // Trims the whitespace from both ends of the string.  Whitespace is defined by
        // Char.IsWhiteSpace.
        //
        public String Trim()
        {
            return TrimHelper(TrimBoth);
        }

        private String TrimHelper(int trimType)
        {
            //end will point to the first non-trimmed character on the right
            //start will point to the first non-trimmed character on the Left
            int end = this.Length - 1;
            int start = 0;

            //Trim specified characters.
            if (trimType != TrimTail)
            {
                for (start = 0; start < this.Length; start++)
                {
                    if (!Char.IsWhiteSpace(this[start])) break;
                }
            }

            if (trimType != TrimHead)
            {
                for (end = Length - 1; end >= start; end--)
                {
                    if (!Char.IsWhiteSpace(this[end])) break;
                }
            }

            return CreateTrimmedString(start, end);
        }

        private String TrimHelper(char[] trimChars, int trimType)
        {
            //end will point to the first non-trimmed character on the right
            //start will point to the first non-trimmed character on the Left
            int end = this.Length - 1;
            int start = 0;

            //Trim specified characters.
            if (trimType != TrimTail)
            {
                for (start = 0; start < this.Length; start++)
                {
                    int i = 0;
                    char ch = this[start];
                    for (i = 0; i < trimChars.Length; i++)
                    {
                        if (trimChars[i] == ch) break;
                    }
                    if (i == trimChars.Length)
                    { // the character is not white space
                        break;
                    }
                }
            }

            if (trimType != TrimHead)
            {
                for (end = Length - 1; end >= start; end--)
                {
                    int i = 0;
                    char ch = this[end];
                    for (i = 0; i < trimChars.Length; i++)
                    {
                        if (trimChars[i] == ch) break;
                    }
                    if (i == trimChars.Length)
                    { // the character is not white space
                        break;
                    }
                }
            }

            return CreateTrimmedString(start, end);
        }

        private String CreateTrimmedString(int start, int end)
        {
            int len = end - start + 1;
            if (len == this.Length)
            {
                // Don't allocate a new string as the trimmed string has not changed.
                return this;
            }
            else
            {
                if (len == 0)
                {
                    return String.Empty;
                }
                return InternalSubString(start, len);
            }
        }
    }
}