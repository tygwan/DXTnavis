using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DXBase.Models
{
    /// <summary>
    /// Utility for generating normalized project codes from project names.
    /// Phase C.2 (Green) - TDD Implementation
    /// </summary>
    public static class ProjectCodeUtil
    {
        private const string FallbackCode = "PROJECT_UNKNOWN";

        /// <summary>
        /// Generates a normalized project code from a project name.
        ///
        /// Rules:
        /// - Converts to uppercase
        /// - Replaces spaces with underscores
        /// - Removes special characters except underscores and numbers
        /// - Romanizes Korean characters
        /// - Returns PROJECT_UNKNOWN for null/empty inputs
        /// </summary>
        /// <param name="projectName">The project name to normalize</param>
        /// <returns>Normalized project code</returns>
        public static string Generate(string projectName)
        {
            // Handle null/empty/whitespace inputs
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return FallbackCode;
            }

            // Trim and romanize Korean characters
            string normalized = RomanizeKorean(projectName.Trim());

            // Replace special separator characters (dashes, parentheses, etc.) with spaces
            normalized = Regex.Replace(normalized, @"[-()[\]{}.,;:!?]+", " ");

            // Replace multiple spaces with single space
            normalized = Regex.Replace(normalized, @"\s+", " ");

            // Trim again to remove any leading/trailing spaces from separator replacements
            normalized = normalized.Trim();

            // Replace spaces with underscores
            normalized = normalized.Replace(" ", "_");

            // Remove any remaining special characters, keep only letters, numbers, and underscores
            normalized = Regex.Replace(normalized, @"[^A-Za-z0-9_]", "");

            // Convert to uppercase
            normalized = normalized.ToUpperInvariant();

            // Return fallback if result is empty
            return string.IsNullOrEmpty(normalized) ? FallbackCode : normalized;
        }

        /// <summary>
        /// Romanizes Korean characters to English equivalents.
        /// Uses a simple mapping for common Korean syllables.
        /// </summary>
        private static string RomanizeKorean(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder();

            foreach (char c in input)
            {
                // Check if character is Hangul (Korean)
                if (c >= 0xAC00 && c <= 0xD7A3)
                {
                    // Decompose Hangul syllable into onset, nucleus, coda
                    int syllableIndex = c - 0xAC00;
                    int onset = syllableIndex / 588;  // 21 * 28
                    int nucleus = (syllableIndex % 588) / 28;
                    int coda = syllableIndex % 28;

                    // Append romanized components
                    result.Append(GetOnset(onset));
                    result.Append(GetNucleus(nucleus));
                    if (coda > 0)
                        result.Append(GetCoda(coda));
                }
                else
                {
                    // Not Korean, keep as-is
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        // Korean romanization tables (Revised Romanization of Korean)
        private static readonly string[] Onsets = {
            "g", "kk", "n", "d", "tt", "r", "m", "b", "pp",
            "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h"
        };

        private static readonly string[] Nuclei = {
            "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa",
            "wae", "oe", "yo", "u", "weo", "we", "wi", "yu", "eu", "ui", "i"
        };

        private static readonly string[] Codas = {
            "", "k", "k", "k", "n", "n", "n", "t", "l", "l",
            "l", "l", "l", "l", "l", "l", "m", "p", "t", "t",
            "ng", "t", "t", "k", "t", "t", "p", "t"
        };

        private static string GetOnset(int index) => index < Onsets.Length ? Onsets[index] : "";
        private static string GetNucleus(int index) => index < Nuclei.Length ? Nuclei[index] : "";
        private static string GetCoda(int index) => index < Codas.Length ? Codas[index] : "";
    }
}
