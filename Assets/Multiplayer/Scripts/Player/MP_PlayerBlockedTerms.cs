using System;
using System.Collections.Generic;
using System.Text;

internal static class MP_PlayerBlockedTerms
{
    private static readonly string[] Terms =
    {
        "8=D", "A55hole", "abortion", "ahole",
        "AIDs", "ainuzin", "anal", "anus",
        "anuses", "Anushead", "anuslick", "anuss",
        "aokan", "Arsch", "Arschloch", "arse",
        "arsed", "arsehole", "arseholed", "arseholes",
        "arseholing", "arselicker", "arses", "ass",
        "asshat", "asshole", "Auschwitz", "b00bz",
        "b1tc", "Baise", "bakachon", "balls",
        "Ballsack", "bastard", "BDSM", "Beaner",
        "beaver", "Beeatch", "beeeyotch", "beefwhistle",
        "beeotch", "Beetch", "beeyotch", "Bellend",
        "bestiality", "beyitch", "beyotch", "Biach",
        "bin laden", "binladen", "bint", "biotch",
        "bitch", "Bitching", "bloody", "blowjob",
        "blowme", "blyad", "blyadt", "bollocks",
        "bon3r", "boner", "boobs", "Btch",
        "bugger", "Bukakke", "bukkak", "bullshit",
        "bung", "butagorosi", "Butthole", "Buttplug",
        "c0ck", "Cabron", "Cacca", "Cadela",
        "Cagada", "Cameljockey", "Caralho", "castrate",
        "Cazzo", "ceemen", "ch1nk", "chankoro",
        "chibusa", "chieokure", "chikan", "chikubi",
        "chikusatsu", "Chinchin", "Ching chong", "Chinga",
        "Chingada Madre", "Chingado", "Chingate", "chinge",
        "chink", "chinko", "chinpo", "chinpoko",
        "chitsu", "Chlamydia", "choad", "chode",
        "chonga", "chonko", "chonkoro", "chourimbo",
        "chourinbo", "chourippo", "Christ", "chuurembo",
        "chuurenbo", "circlejerk", "cl1t", "cli7",
        "clit", "clitoris", "cocain", "Cocaine",
        "cock", "cocksucker", "Coglione", "Coglioni",
        "coitus", "coituss", "cojelon", "cojones",
        "cok", "condom", "coon", "coon hunt",
        "coon kill", "coonhunt", "coonkill", "Cooter",
        "cotton pic", "cotton pik", "cottonpic", "cottonpik",
        "Crackhead", "crap", "CSAM", "Culear",
        "Culero", "Culo", "cum", "cumshot",
        "cun7", "cunt", "cvn7", "cvnt",
        "cyka", "d1kc", "d4go", "dago",
        "damn", "Darkie", "Deez Nuts", "deeznut",
        "deeznuts", "dick", "dickhead", "dikc",
        "dildo", "Dio Bestia", "dong", "dongs",
        "douche", "Downie", "Downy", "Dumbass",
        "Durka durka", "dyke", "Ejaculate", "Encule",
        "enjokousai", "enzyokousai", "etahinin", "etambo",
        "etanbo", "f0ck", "f0kc", "f3lch",
        "facking", "fag", "faggot", "Fanculo",
        "fanny", "fart", "fatass", "fck",
        "Fckn", "fcuk", "fcuuk", "felch",
        "Fetish", "Fgt", "Fick", "FiCKDiCH",
        "Figlio di Puttana", "fku", "fock", "fokc",
        "foreskin", "Fotze", "Foutre", "fucc",
        "fuck", "fucker", "Fucking", "fuct",
        "fuk", "fukashokumin", "Funben", "Fupa",
        "fuuck", "fuuuck", "fuuuuck", "fuuuuuck",
        "fuuuuuuck", "fuuuuuuuck", "fuuuuuuuuck", "fuuuuuuuuuck",
        "fuuuuuuuuuu", "fvck", "fxck", "fxuxcxk",
        "g000k", "g00k", "g0ok", "gannsha",
        "gansya", "gaylord", "gestapo", "gimp",
        "git", "go0k", "gobshite", "god",
        "god damn", "goldenshowers", "golliwogg", "gollywog",
        "Gooch", "gook", "goook", "Gyp",
        "gypo", "h0m0", "h0mo",
        "h1tl3", "h1tle", "hairpie", "Hakuchi",
        "hakujakusha", "hakuroubyo", "Hakuti", "hakuzyakusya",
        "Hashishi", "hasisi", "hell", "Herpes",
        "hitl3r", "hitler", "hitlr", "holocaust",
        "hom0", "homo", "honky", "hooker",
        "hor3", "hukasyokumin", "Hunben", "Hure",
        "Hurensohn", "huzinoyamai", "hymen", "inc3st",
        "incest", "Inculato", "Injun", "intercourse",
        "inugoroshi", "inugorosi", "j1g4b0", "j1g4bo",
        "j1gab0", "j1gabo", "Jack Off", "jackass",
        "jap", "JerkOff", "Jesus", "jig4b0",
        "jig4bo", "jigabo", "Jigaboo", "jiggaboo",
        "jii", "jism", "jizz", "Joder",
        "Joto", "Jungle Bunny", "junglebunny", "k k k",
        "k1k3", "Kaffir", "kaishun", "kaisyun",
        "kichigai", "kijirushi", "kijirusi", "kik3",
        "kike", "kikeiji", "kikeizi", "Kilurself",
        "kitigai", "kkk", "klu klux", "Klu Klux Klan",
        "kluklux", "knob", "knobhead", "koon",
        "koon hunt", "koon kill", "koonhunt", "koonkill",
        "koroshiteyaru", "koumoujin", "koumouzin", "kraut",
        "ku klux klan", "kun7", "kurombo", "Kurva",
        "Kurwa", "kxkxk", "kyke", "l3sb0",
        "lesbo", "lezbo", "lezzie", "m07th3rfukr",
        "m0th3rfvk3r", "m0th3rfvker", "Madonna Puttana", "manberries",
        "manko", "manshaft", "Maricon", "Masterbat",
        "masterbate", "Masturbacion", "masturbait", "Masturbare",
        "Masturbate", "Masturbazione", "Merda", "Merde",
        "Meth", "Mierda", "milf", "minge",
        "minger", "mitsukuchi", "mitukuti",
        "Molest", "molester", "molestor", "Mong",
        "Moon Cricket", "moron", "moth3rfucer", "moth3rfvk3r",
        "moth3rfvker", "motherfucker", "motherfuckers", "muff",
        "Mulatto", "mutha", "n1663r", "n1664",
        "n166a", "n166er", "n166ers", "n1g3r",
        "n1German", "n1gg3r", "n1gg3rs", "n1gger",
        "n1gGerman", "n1ggers", "n3gro", "n4g3r",
        "n4gg3r", "n4gGerman", "n4z1", "nag3r",
        "nagg3r", "nagGerman", "natzi", "naz1",
        "Nazi", "Nazis", "nazl", "neGerman",
        "ngGerman", "nggr", "NhigGerman", "ni666",
        "ni66a", "ni66er", "ni66g", "ni6g",
        "ni6g6", "ni6gg", "nig", "nig66",
        "nig6g", "nigar", "niGerman", "nigg3",
        "nigg6", "nigga", "niggaz", "nigger",
        "nigGerman", "niggers", "nigglet", "niggr",
        "nigguh", "niggur", "niggy", "niglet",
        "Nignog", "nimpinin", "ninpinin", "nip",
        "Nipples", "niqqa", "niqqer", "nonce",
        "nugga", "Nutsack", "Nutted", "nygGerman",
        "omeko", "Orgy", "p3n15", "p3n1s",
        "p3ni5", "p3nis", "p3nl5", "p3nls",
        "paedo", "paedophile", "paki", "Panties",
        "pecker", "pedo", "pedoph", "pedophile",
        "pen15", "pen1s", "Pendejo", "peni5",
        "penile", "penis", "penl5", "penls",
        "penus", "Perra", "phaggot", "phagot",
        "phuck", "pikey", "pimp", "Pinche",
        "piss", "Pizda", "Polla", "ponce",
        "poof", "Porca Madonna", "Porch monkey", "porn",
        "Porra", "pr1ck", "preteen", "prick",
        "pu555y", "pu55y", "pub1c", "Pube",
        "pubic", "pun4ni", "pun4nl", "Punal",
        "punan1", "punani", "punanl", "puss1",
        "puss3", "puss5", "pusse", "pussi",
        "Pussies", "pusss1", "pussse", "pusssi",
        "pusssl", "pusssy", "pussy", "Puta",
        "Putain", "Pute", "Puto", "Puttana",
        "Puttane", "Puttaniere", "puzzy", "pvssy",
        "queef", "queer", "r3c7um", "r4p15t",
        "r4p1st", "r4p3", "r4pi5t", "r4pist",
        "raape", "raghead", "raibyo", "Raip",
        "rap15t", "rap1st", "Rapage", "rape",
        "Raped", "rapi5t", "Raping", "rapist",
        "rectum", "Red Tube", "Reggin", "reipu",
        "rentboy", "retard", "Ricchione", "rimjob",
        "rizzape", "rompari", "Salaud", "Salope",
        "sangokujin", "sangokuzin", "satsujin", "satujin",
        "Schlampe", "schlong", "Schwuchtel",
        "screw", "Scrote", "secks", "seishinhakujaku",
        "seishinijo", "seisinhakuzyaku", "seisinizyo", "Semen",
        "semushi", "semushiotoko", "semusi", "semusiotoko",
        "SEX", "sh17", "sh1t", "shag",
        "shagged", "shasei", "Shat", "Shemale",
        "shi7", "shinajin", "shine", "shinheimin",
        "shirakko", "shit", "Shitty", "shl7",
        "shlt", "shojo", "shokubutsuningen", "sinazin",
        "sine", "sinheimin", "Skank", "slag",
        "slapper", "slut", "SMD", "snatch",
        "sod", "Sodom", "sofa king", "sofaking",
        "son-of-a-bitch", "Spanishick", "Spanishook", "Spanishunk",
        "spas", "spastic", "spaz", "sperm",
        "spick", "spik", "spunk", "STD",
        "STDs", "Succhia Cazzi", "suck my", "suckmy",
        "syasei", "syojo", "syokubutuningen", "Taint",
        "Tampon", "Tapatte", "Tapette", "Tard",
        "Tarlouse", "tart", "teensex", "teino",
        "Testa di Cazzo", "Testicles", "Thot", "tibusa",
        "tieokure", "tikan", "tikubi", "tinge",
        "Tinkasu", "Tinko", "tinnpoko", "tinpo",
        "tintin", "Tits", "titu", "tokushugakkyu",
        "tokusyugakkyu", "torukoburo", "torukojo", "torukozyo",
        "tosatsu", "tosatu", "tosser", "towelhead",
        "Tranny", "tunbo", "turd", "tw47",
        "tw4t", "twat", "tyankoro", "tyonga",
        "tyonko", "tyonkoro", "tyourinbo", "tyourippo",
        "tyurenbo", "unchi", "unko", "unti",
        "ushigoroshi", "usigorosi", "v461n4", "v461na",
        "v46in4", "v46ina", "v4g1n4", "v4g1na",
        "v4gin4", "v4gina", "va61n4", "va61na",
        "va6in4", "va6ina", "Vaccagare", "Vaffanculo",
        "Vag", "vag1n4", "vag1na", "vagin4",
        "vagina", "VateFaire", "vvhitepower", "w3tb4ck",
        "w3tback", "wank", "wanker", "warumoji",
        "wetb4ck", "wetback", "wh0r3", "wh0re",
        "white power", "whitepower", "whor3", "whore",
        "wog", "wop", "x8lp3t", "xbl pet",
        "XBLPET", "XBLRewards", "Xl3LPET", "yabunirami",
        "zii", "Zipperhead",
    };

    private static readonly string[] SearchTerms;

    static MP_PlayerBlockedTerms()
    {
        HashSet<string> normalizedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string term in Terms)
        {
            string normalized = CompactForMatch(term);
            if (normalized.Length > 0)
                normalizedTerms.Add(normalized);
        }

        SearchTerms = new string[normalizedTerms.Count];
        normalizedTerms.CopyTo(SearchTerms);
    }

    public static bool ContainsIn(string name)
    {
        string normalized = CompactForMatch(name);
        if (ContainsTerm(normalized) || ContainsTermWithRepeatedLetters(normalized))
            return true;

        StringBuilder withoutDigits = new StringBuilder(normalized.Length);
        bool hasDigit = false;
        foreach (char character in normalized)
        {
            if (character >= '0' && character <= '9')
                hasDigit = true;
            else
                withoutDigits.Append(character);
        }

        string lettersOnly = withoutDigits.ToString();
        return hasDigit && (ContainsTerm(lettersOnly) ||
            ContainsTermWithRepeatedLetters(lettersOnly) ||
            ContainsTermWithLookalikes(normalized));
    }

    private static bool ContainsTerm(string name)
    {
        foreach (string term in SearchTerms)
        {
            if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool ContainsTermWithRepeatedLetters(string name)
    {
        bool hasRepeatedLetter = false;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsLetter(name[i]) &&
                char.ToLowerInvariant(name[i]) == char.ToLowerInvariant(name[i - 1]))
            {
                hasRepeatedLetter = true;
                break;
            }
        }

        if (!hasRepeatedLetter)
            return false;

        foreach (string term in SearchTerms)
        {
            for (int start = 0; start < name.Length; start++)
            {
                int nameIndex = start;
                int termIndex = 0;
                bool matched = true;
                while (nameIndex < name.Length && termIndex < term.Length)
                {
                    char character = char.ToLowerInvariant(term[termIndex]);
                    if (char.ToLowerInvariant(name[nameIndex]) != character)
                    {
                        matched = false;
                        break;
                    }

                    int requiredCount = 0;
                    while (termIndex < term.Length &&
                        char.ToLowerInvariant(term[termIndex]) == character)
                    {
                        termIndex++;
                        requiredCount++;
                    }

                    int actualCount = 0;
                    while (nameIndex < name.Length &&
                        char.ToLowerInvariant(name[nameIndex]) == character)
                    {
                        nameIndex++;
                        actualCount++;
                    }

                    if (actualCount < requiredCount ||
                        (!char.IsLetter(character) && actualCount != requiredCount))
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched && termIndex == term.Length)
                    return true;
            }
        }

        return false;
    }

    private static bool ContainsTermWithLookalikes(string name)
    {
        foreach (string term in SearchTerms)
        {
            for (int start = 0; start <= name.Length - term.Length; start++)
            {
                int index = 0;
                for (; index < term.Length; index++)
                {
                    char actual = char.ToLowerInvariant(name[start + index]);
                    char expected = char.ToLowerInvariant(term[index]);
                    if (actual != expected && !DigitLooksLikeLetter(actual, expected))
                        break;
                }

                if (index == term.Length)
                    return true;
            }
        }

        return false;
    }

    private static bool DigitLooksLikeLetter(char digit, char letter)
    {
        switch (digit)
        {
            case '0': return letter == 'o';
            case '1': return letter == 'i' || letter == 'l';
            case '2': return letter == 'z';
            case '3': return letter == 'e';
            case '4': return letter == 'a';
            case '5': return letter == 's';
            case '6': return letter == 'b' || letter == 'g';
            case '7': return letter == 't';
            case '8': return letter == 'b';
            case '9': return letter == 'g' || letter == 'q';
            default: return false;
        }
    }

    private static string CompactForMatch(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.ToString();
    }
}
