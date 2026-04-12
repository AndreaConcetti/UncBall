using System;
using System.Collections.Generic;
using UnityEngine;

public static class BotNameLibrary
{
    // Curated multilingual seed pool inspired by widely used contemporary names across several regions.
    // The pool is then expanded algorithmically into 500+ platform-safe display names.
    private static readonly string[] coreNames =
    {
        // IT / EU
        "Leonardo","Sofia","Edoardo","Aurora","Tommaso","Giulia","Alessandro","Ginevra","Luca","Matilde",
        "Matteo","Beatrice","Davide","Vittoria","Andrea","Alice","Riccardo","Emma","Lorenzo","Greta",
        "Noah","Olivia","Oliver","Amelia","Theo","Isla","Leo","Mia","Jack","Ava",
        "Arthur","Ella","Oscar","Freya","Lucas","Lily","Henry","Ivy","Finley","Grace",
        "Luka","Nika","Nik","Lena","Jakub","Mila","Filip","Nora","Samuel","Elena",

        // Iberia / LATAM
        "Mateo","Santiago","Thiago","Valentina","Gael","Sofia","Lucas","Martina","Bruno","Emilia",
        "Enzo","Camila","Benjamin","Julieta","Rafael","Lucia","Diego","Renata","Nicolas","Mia",
        "Sebastian","Zoe","Agustin","Elena","Facundo","Alma","Dante","Aitana","Liam","Salome",

        // Brazil / Lusophone
        "Miguel","Helena","Ravi","Alice","Gael","Cecilia","Theo","Laura","Davi","Maria",
        "Heitor","Heloisa","Arthur","Liz","Noah","Maitê","Murilo","Yasmin","Caio","Luna",

        // North America
        "Liam","Emma","James","Charlotte","William","Sophia","Elijah","Evelyn","Lucas","Harper",
        "Mason","Abigail","Ezra","Scarlett","Logan","Layla","Owen","Chloe","Levi","Aria",

        // Eastern Europe / Balkans
        "Nikola","Anastasia","Marko","Ana","Milos","Sofija","Stefan","Teodora","Pavle","Milica",
        "Bogdan","Eva","Lazar","Mariya","Vuk","Kateryna","Aleksa","Varvara","Dušan","Polina",

        // MENA / Arabic-origin names
        "Muhammad","Amina","Omar","Layla","Yusuf","Nour","Adam","Mariam","Zayd","Sara",
        "Rayyan","Inaya","Amir","Aya","Karim","Leen","Ilyas","Yara","Samir","Nadia",

        // South Asia
        "Aarav","Anaya","Arjun","Diya","Kabir","Aisha","Krish","Ira","Rohan","Kiara",
        "Vihaan","Mira","Ishaan","Myra","Reyansh","Saanvi","Dhruv","Tara","Advik","Navya",

        // East / Southeast Asia romanized
        "Haruto","Yui","Minato","Aoi","Ren","Mei","Sora","Hina","Yuto","Ema",
        "Itsuki","Rin","Haru","Akari","Kaito","Yuna","Nagi","Himari","Takumi","Kana",
        "Jin","Sumi","Haruo","Mika","Kenji","Rika","Daichi","Airi","Yuki","Nao",

        // Global modern gamer-safe names
        "Nyx","Kaido","Riven","Axel","Orion","Kairo","Zeno","Vex","Taron","Blaze",
        "Nova","Kai","Milo","Skye","Zara","Arlo","Lyra","Nox","Juno","Astra"
    };

    private static readonly string[] optionalPrefixes =
    {
        "Neo","Dark","Ultra","Prime","Pro","x","Mr","Nova","Zero","Night"
    };

    private static readonly string[] optionalSuffixes =
    {
        "X","EX","Prime","JR","One","V","XV","Nova","Go","Max"
    };

    private static readonly int[] numberSuffixes =
    {
        7, 8, 9, 10, 11, 12, 13, 17, 21, 22, 24, 27, 31, 33, 47, 51, 64, 66, 71, 77, 88, 90, 99
    };

    private static List<string> cachedPool;

    public static IReadOnlyList<string> GetDefaultGeneratedPool(int minimumCount = 500)
    {
        minimumCount = Mathf.Max(100, minimumCount);

        if (cachedPool == null || cachedPool.Count < minimumCount)
        {
            cachedPool = BuildPool(minimumCount);
        }

        return cachedPool;
    }

    private static List<string> BuildPool(int minimumCount)
    {
        HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> ordered = new List<string>(minimumCount + 128);

        void AddCandidate(string value)
        {
            string sanitized = Sanitize(value);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return;
            }

            if (unique.Add(sanitized))
            {
                ordered.Add(sanitized);
            }
        }

        for (int i = 0; i < coreNames.Length; i++)
        {
            string baseName = coreNames[i];

            AddCandidate(baseName);
            AddCandidate(baseName + "X");
            AddCandidate(baseName + "V");
            AddCandidate(baseName + "JR");

            string shortName = baseName.Length > 4 ? baseName.Substring(0, Mathf.Min(baseName.Length, 4)) : baseName;
            AddCandidate(shortName + "on");
            AddCandidate(shortName + "ix");
            AddCandidate(shortName + "ar");

            for (int p = 0; p < optionalPrefixes.Length; p++)
            {
                AddCandidate(optionalPrefixes[p] + baseName);
            }

            for (int s = 0; s < optionalSuffixes.Length; s++)
            {
                AddCandidate(baseName + optionalSuffixes[s]);
            }

            for (int n = 0; n < numberSuffixes.Length; n += 4)
            {
                AddCandidate(baseName + numberSuffixes[n]);
            }
        }

        for (int i = 0; i < coreNames.Length; i++)
        {
            string first = coreNames[i];
            string second = coreNames[(i + 17) % coreNames.Length];

            string firstShort = first.Substring(0, Mathf.Min(first.Length, 3));
            string secondShort = second.Substring(0, Mathf.Min(second.Length, 3));

            AddCandidate(firstShort + secondShort);
            AddCandidate(first + secondShort);
            AddCandidate(firstShort + second);
        }

        // Safety fill in case sanitation removes too many names.
        int index = 0;
        while (ordered.Count < minimumCount)
        {
            string generated = $"Player{1000 + index}";
            AddCandidate(generated);
            index++;
        }

        return ordered;
    }

    private static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string trimmed = raw.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
        List<char> chars = new List<char>(trimmed.Length);

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (char.IsLetterOrDigit(c))
            {
                chars.Add(c);
            }
        }

        if (chars.Count == 0)
        {
            return string.Empty;
        }

        string result = new string(chars.ToArray());
        if (result.Length > 16)
        {
            result = result.Substring(0, 16);
        }

        return result;
    }
}
