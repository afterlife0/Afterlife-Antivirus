using System;
using System.Runtime.InteropServices;

namespace AfterlifeWinUI.Services;

/// <summary>
/// Native P/Invoke declarations for libyara64.dll
/// YARA version 4.x API
/// </summary>
internal static class YaraNative
{
    private const string LibraryName = "libyara64.dll";

    // Error codes
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_INSUFFICIENT_MEMORY = 1;
    public const int ERROR_COULD_NOT_ATTACH_TO_PROCESS = 2;
    public const int ERROR_COULD_NOT_OPEN_FILE = 3;
    public const int ERROR_COULD_NOT_MAP_FILE = 4;
    public const int ERROR_INVALID_FILE = 6;
    public const int ERROR_CORRUPT_FILE = 7;
    public const int ERROR_UNSUPPORTED_FILE_VERSION = 8;
    public const int ERROR_INVALID_REGULAR_EXPRESSION = 9;
    public const int ERROR_INVALID_HEX_STRING = 10;
    public const int ERROR_SYNTAX_ERROR = 11;
    public const int ERROR_LOOP_NESTING_LIMIT_EXCEEDED = 12;
    public const int ERROR_DUPLICATED_LOOP_IDENTIFIER = 13;
    public const int ERROR_DUPLICATED_IDENTIFIER = 14;
    public const int ERROR_DUPLICATED_TAG_IDENTIFIER = 15;
    public const int ERROR_DUPLICATED_META_IDENTIFIER = 16;
    public const int ERROR_DUPLICATED_STRING_IDENTIFIER = 17;
    public const int ERROR_UNREFERENCED_STRING = 18;
    public const int ERROR_UNDEFINED_STRING = 19;
    public const int ERROR_UNDEFINED_IDENTIFIER = 20;
    public const int ERROR_MISPLACED_ANONYMOUS_STRING = 21;
    public const int ERROR_INCLUDES_CIRCULAR_REFERENCE = 22;
    public const int ERROR_INCLUDE_DEPTH_EXCEEDED = 23;
    public const int ERROR_WRONG_TYPE = 24;
    public const int ERROR_EXEC_STACK_OVERFLOW = 25;
    public const int ERROR_SCAN_TIMEOUT = 26;
    public const int ERROR_TOO_MANY_SCAN_THREADS = 27;
    public const int ERROR_CALLBACK_ERROR = 28;
    public const int ERROR_INVALID_ARGUMENT = 29;
    public const int ERROR_TOO_MANY_MATCHES = 30;
    public const int ERROR_INTERNAL_FATAL_ERROR = 31;
    public const int ERROR_NESTED_FOR_OF_LOOP = 32;
    public const int ERROR_INVALID_FIELD_NAME = 33;
    public const int ERROR_UNKNOWN_MODULE = 34;
    public const int ERROR_NOT_A_STRUCTURE = 35;
    public const int ERROR_NOT_INDEXABLE = 36;
    public const int ERROR_NOT_A_FUNCTION = 37;
    public const int ERROR_INVALID_FORMAT = 38;
    public const int ERROR_TOO_MANY_ARGUMENTS = 39;
    public const int ERROR_WRONG_ARGUMENTS = 40;
    public const int ERROR_WRONG_RETURN_TYPE = 41;
    public const int ERROR_DUPLICATED_STRUCTURE_MEMBER = 42;
    public const int ERROR_EMPTY_STRING = 43;
    public const int ERROR_DIVISION_BY_ZERO = 44;
    public const int ERROR_REGULAR_EXPRESSION_TOO_LARGE = 45;
    public const int ERROR_TOO_MANY_RE_FIBERS = 46;
    public const int ERROR_COULD_NOT_READ_PROCESS_MEMORY = 47;
    public const int ERROR_INVALID_EXTERNAL_VARIABLE_TYPE = 48;
    public const int ERROR_REGULAR_EXPRESSION_TOO_COMPLEX = 49;
    public const int ERROR_INVALID_MODULE_NAME = 50;
    public const int ERROR_TOO_MANY_STRINGS = 51;
    public const int ERROR_INTEGER_OVERFLOW = 52;
    public const int ERROR_CALLBACK_REQUIRED = 53;
    public const int ERROR_INVALID_OPERAND = 54;
    public const int ERROR_COULD_NOT_READ_FILE = 55;
    public const int ERROR_DUPLICATED_EXTERNAL_VARIABLE = 56;
    public const int ERROR_INVALID_MODULE_DATA = 57;
    public const int ERROR_WRITING_FILE = 58;
    public const int ERROR_INVALID_MODIFIER = 59;
    public const int ERROR_DUPLICATED_MODIFIER = 60;
    public const int ERROR_BLOCK_NOT_READY = 61;
    public const int ERROR_INVALID_PERCENTAGE = 62;
    public const int ERROR_IDENTIFIER_MATCHES_WILDCARD = 63;
    public const int ERROR_INVALID_VALUE = 64;

    // Callback message types
    public const int CALLBACK_MSG_RULE_MATCHING = 1;
    public const int CALLBACK_MSG_RULE_NOT_MATCHING = 2;
    public const int CALLBACK_MSG_SCAN_FINISHED = 3;
    public const int CALLBACK_MSG_IMPORT_MODULE = 4;
    public const int CALLBACK_MSG_MODULE_IMPORTED = 5;
    public const int CALLBACK_MSG_TOO_MANY_MATCHES = 6;
    public const int CALLBACK_MSG_CONSOLE_LOG = 7;

    // Callback return values
    public const int CALLBACK_CONTINUE = 0;
    public const int CALLBACK_ABORT = 1;
    public const int CALLBACK_ERROR = 2;

    // Scan flags
    public const int SCAN_FLAGS_FAST_MODE = 1;
    public const int SCAN_FLAGS_PROCESS_MEMORY = 2;
    public const int SCAN_FLAGS_NO_TRYCATCH = 4;
    public const int SCAN_FLAGS_REPORT_RULES_MATCHING = 8;
    public const int SCAN_FLAGS_REPORT_RULES_NOT_MATCHING = 16;

    /// <summary>
    /// YARA rule structure (simplified for callback)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct YR_RULE
    {
        public int g_flags;
        public int t_flags_0;
        public int t_flags_1;
        public IntPtr identifier;  // const char*
        public IntPtr tags;        // const char*
        public IntPtr metas;       // YR_META*
        public IntPtr strings;     // YR_STRING*
        public IntPtr ns;          // YR_NAMESPACE*
        public int num_atoms;
        public int time_cost;
        public long time_cost_per_thread_0;
        public long time_cost_per_thread_1;
    }

    /// <summary>
    /// Callback delegate for scan results
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int YR_CALLBACK_FUNC(
        IntPtr context,
        int message,
        IntPtr message_data,
        IntPtr user_data);

    /// <summary>
    /// Compiler error callback delegate
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void YR_COMPILER_CALLBACK_FUNC(
        int error_level,
        [MarshalAs(UnmanagedType.LPStr)] string file_name,
        int line_number,
        IntPtr rule,
        [MarshalAs(UnmanagedType.LPStr)] string message,
        IntPtr user_data);

    // ========== YARA Library Functions ==========

    /// <summary>
    /// Initialize YARA library. Must be called before any other function.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int yr_initialize();

    /// <summary>
    /// Finalize YARA library. Must be called when done.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int yr_finalize();

    // ========== Compiler Functions ==========

    /// <summary>
    /// Create a new compiler
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int yr_compiler_create(out IntPtr compiler);

    /// <summary>
    /// Destroy a compiler
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void yr_compiler_destroy(IntPtr compiler);

    /// <summary>
    /// Set error callback for compiler
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void yr_compiler_set_callback(
        IntPtr compiler,
        YR_COMPILER_CALLBACK_FUNC callback,
        IntPtr user_data);

    /// <summary>
    /// Add rules from a file to the compiler
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int yr_compiler_add_file(
        IntPtr compiler,
        IntPtr file,
        [MarshalAs(UnmanagedType.LPStr)] string? ns,
        [MarshalAs(UnmanagedType.LPStr)] string file_name);

    /// <summary>
    /// Add rules from a string to the compiler
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int yr_compiler_add_string(
        IntPtr compiler,
        [MarshalAs(UnmanagedType.LPStr)] string rules_string,
        [MarshalAs(UnmanagedType.LPStr)] string? ns);

    /// <summary>
    /// Get compiled rules from compiler
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int yr_compiler_get_rules(
        IntPtr compiler,
        out IntPtr rules);

    // ========== Rules Functions ==========

    /// <summary>
    /// Destroy compiled rules
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int yr_rules_destroy(IntPtr rules);

    /// <summary>
    /// Scan a file with compiled rules
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int yr_rules_scan_file(
        IntPtr rules,
        [MarshalAs(UnmanagedType.LPStr)] string filename,
        int flags,
        YR_CALLBACK_FUNC callback,
        IntPtr user_data,
        int timeout);

    /// <summary>
    /// Scan memory buffer with compiled rules
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int yr_rules_scan_mem(
        IntPtr rules,
        IntPtr buffer,
        UIntPtr buffer_size,
        int flags,
        YR_CALLBACK_FUNC callback,
        IntPtr user_data,
        int timeout);

    /// <summary>
    /// Save compiled rules to a file
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int yr_rules_save(
        IntPtr rules,
        [MarshalAs(UnmanagedType.LPStr)] string filename);

    /// <summary>
    /// Load compiled rules from a file
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int yr_rules_load(
        [MarshalAs(UnmanagedType.LPStr)] string filename,
        out IntPtr rules);

    // ========== Scanner Functions (YARA 4.x) ==========

    /// <summary>
    /// Create a scanner from compiled rules
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int yr_scanner_create(
        IntPtr rules,
        out IntPtr scanner);

    /// <summary>
    /// Destroy a scanner
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void yr_scanner_destroy(IntPtr scanner);

    /// <summary>
    /// Set callback for scanner
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void yr_scanner_set_callback(
        IntPtr scanner,
        YR_CALLBACK_FUNC callback,
        IntPtr user_data);

    /// <summary>
    /// Set timeout for scanner
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void yr_scanner_set_timeout(
        IntPtr scanner,
        int timeout);

    /// <summary>
    /// Set scan flags for scanner
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void yr_scanner_set_flags(
        IntPtr scanner,
        int flags);

    /// <summary>
    /// Scan a file with scanner
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int yr_scanner_scan_file(
        IntPtr scanner,
        [MarshalAs(UnmanagedType.LPStr)] string filename);

    /// <summary>
    /// Scan memory with scanner
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int yr_scanner_scan_mem(
        IntPtr scanner,
        IntPtr buffer,
        UIntPtr buffer_size);

    // ========== Helper Methods ==========

    /// <summary>
    /// Get error message for error code
    /// </summary>
    public static string GetErrorMessage(int errorCode)
    {
        return errorCode switch
        {
            ERROR_SUCCESS => "Success",
            ERROR_INSUFFICIENT_MEMORY => "Insufficient memory",
            ERROR_COULD_NOT_OPEN_FILE => "Could not open file",
            ERROR_COULD_NOT_MAP_FILE => "Could not map file",
            ERROR_INVALID_FILE => "Invalid file",
            ERROR_CORRUPT_FILE => "Corrupt file",
            ERROR_SYNTAX_ERROR => "Syntax error in rules",
            ERROR_INVALID_REGULAR_EXPRESSION => "Invalid regular expression",
            ERROR_SCAN_TIMEOUT => "Scan timeout",
            ERROR_CALLBACK_ERROR => "Callback error",
            ERROR_TOO_MANY_MATCHES => "Too many matches",
            _ => $"Error code {errorCode}"
        };
    }
}
