namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>
/// The countries of ISO 3166-1 by their three-letter (alpha-3) code, with their short English names: the EU's battery
/// format names the manufacturing country this way (<c>manufacturingCountry</c>, <c>^[A-Z]{3}$</c>). The list is the
/// app's own, so the page shows the same names on every server.
/// </summary>
public static class Countries
{
    /// <summary>Code to name, in the order of the names.</summary>
    public static IReadOnlyDictionary<string, string> Names { get; } = Build();

    /// <summary>The codes, in the order of their names (as the page offers them).</summary>
    public static IReadOnlyList<string> Codes { get; } = Names.Keys.ToArray();

    /// <summary>True for a code of the list, in capitals as the EU writes it ("SWE").</summary>
    public static bool IsCode(string? code) => code is not null && Names.ContainsKey(code);

    /// <summary>The code of a country typed as its code or its name, capitals not minding ("swe", "Sweden"); null when none matches.</summary>
    public static string? Find(string? typed)
    {
        var text = typed?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var upper = text.ToUpperInvariant();
        if (Names.ContainsKey(upper))
        {
            return upper;
        }

        return Names.FirstOrDefault(pair => string.Equals(pair.Value, text, StringComparison.OrdinalIgnoreCase)).Key;
    }

    private static Dictionary<string, string> Build()
    {
        (string Code, string Name)[] list =
        [
            ("AFG", "Afghanistan"), ("ALA", "Åland Islands"), ("ALB", "Albania"), ("DZA", "Algeria"), ("ASM", "American Samoa"),
            ("AND", "Andorra"), ("AGO", "Angola"), ("AIA", "Anguilla"), ("ATA", "Antarctica"), ("ATG", "Antigua and Barbuda"),
            ("ARG", "Argentina"), ("ARM", "Armenia"), ("ABW", "Aruba"), ("AUS", "Australia"), ("AUT", "Austria"),
            ("AZE", "Azerbaijan"), ("BHS", "Bahamas"), ("BHR", "Bahrain"), ("BGD", "Bangladesh"), ("BRB", "Barbados"),
            ("BLR", "Belarus"), ("BEL", "Belgium"), ("BLZ", "Belize"), ("BEN", "Benin"), ("BMU", "Bermuda"),
            ("BTN", "Bhutan"), ("BOL", "Bolivia"), ("BES", "Bonaire, Sint Eustatius and Saba"), ("BIH", "Bosnia and Herzegovina"), ("BWA", "Botswana"),
            ("BVT", "Bouvet Island"), ("BRA", "Brazil"), ("IOT", "British Indian Ocean Territory"), ("BRN", "Brunei Darussalam"), ("BGR", "Bulgaria"),
            ("BFA", "Burkina Faso"), ("BDI", "Burundi"), ("CPV", "Cabo Verde"), ("KHM", "Cambodia"), ("CMR", "Cameroon"),
            ("CAN", "Canada"), ("CYM", "Cayman Islands"), ("CAF", "Central African Republic"), ("TCD", "Chad"), ("CHL", "Chile"),
            ("CHN", "China"), ("CXR", "Christmas Island"), ("CCK", "Cocos (Keeling) Islands"), ("COL", "Colombia"), ("COM", "Comoros"),
            ("COG", "Congo"), ("COD", "Congo, Democratic Republic of the"), ("COK", "Cook Islands"), ("CRI", "Costa Rica"), ("CIV", "Côte d'Ivoire"),
            ("HRV", "Croatia"), ("CUB", "Cuba"), ("CUW", "Curaçao"), ("CYP", "Cyprus"), ("CZE", "Czechia"),
            ("DNK", "Denmark"), ("DJI", "Djibouti"), ("DMA", "Dominica"), ("DOM", "Dominican Republic"), ("ECU", "Ecuador"),
            ("EGY", "Egypt"), ("SLV", "El Salvador"), ("GNQ", "Equatorial Guinea"), ("ERI", "Eritrea"), ("EST", "Estonia"),
            ("SWZ", "Eswatini"), ("ETH", "Ethiopia"), ("FLK", "Falkland Islands (Malvinas)"), ("FRO", "Faroe Islands"), ("FJI", "Fiji"),
            ("FIN", "Finland"), ("FRA", "France"), ("GUF", "French Guiana"), ("PYF", "French Polynesia"), ("ATF", "French Southern Territories"),
            ("GAB", "Gabon"), ("GMB", "Gambia"), ("GEO", "Georgia"), ("DEU", "Germany"), ("GHA", "Ghana"),
            ("GIB", "Gibraltar"), ("GRC", "Greece"), ("GRL", "Greenland"), ("GRD", "Grenada"), ("GLP", "Guadeloupe"),
            ("GUM", "Guam"), ("GTM", "Guatemala"), ("GGY", "Guernsey"), ("GIN", "Guinea"), ("GNB", "Guinea-Bissau"),
            ("GUY", "Guyana"), ("HTI", "Haiti"), ("HMD", "Heard Island and McDonald Islands"), ("VAT", "Holy See"), ("HND", "Honduras"),
            ("HKG", "Hong Kong"), ("HUN", "Hungary"), ("ISL", "Iceland"), ("IND", "India"), ("IDN", "Indonesia"),
            ("IRN", "Iran"), ("IRQ", "Iraq"), ("IRL", "Ireland"), ("IMN", "Isle of Man"), ("ISR", "Israel"),
            ("ITA", "Italy"), ("JAM", "Jamaica"), ("JPN", "Japan"), ("JEY", "Jersey"), ("JOR", "Jordan"),
            ("KAZ", "Kazakhstan"), ("KEN", "Kenya"), ("KIR", "Kiribati"), ("PRK", "Korea, Democratic People's Republic of"), ("KOR", "Korea, Republic of"),
            ("KWT", "Kuwait"), ("KGZ", "Kyrgyzstan"), ("LAO", "Lao People's Democratic Republic"), ("LVA", "Latvia"), ("LBN", "Lebanon"),
            ("LSO", "Lesotho"), ("LBR", "Liberia"), ("LBY", "Libya"), ("LIE", "Liechtenstein"), ("LTU", "Lithuania"),
            ("LUX", "Luxembourg"), ("MAC", "Macao"), ("MDG", "Madagascar"), ("MWI", "Malawi"), ("MYS", "Malaysia"),
            ("MDV", "Maldives"), ("MLI", "Mali"), ("MLT", "Malta"), ("MHL", "Marshall Islands"), ("MTQ", "Martinique"),
            ("MRT", "Mauritania"), ("MUS", "Mauritius"), ("MYT", "Mayotte"), ("MEX", "Mexico"), ("FSM", "Micronesia"),
            ("MDA", "Moldova"), ("MCO", "Monaco"), ("MNG", "Mongolia"), ("MNE", "Montenegro"), ("MSR", "Montserrat"),
            ("MAR", "Morocco"), ("MOZ", "Mozambique"), ("MMR", "Myanmar"), ("NAM", "Namibia"), ("NRU", "Nauru"),
            ("NPL", "Nepal"), ("NLD", "Netherlands"), ("NCL", "New Caledonia"), ("NZL", "New Zealand"), ("NIC", "Nicaragua"),
            ("NER", "Niger"), ("NGA", "Nigeria"), ("NIU", "Niue"), ("NFK", "Norfolk Island"), ("MKD", "North Macedonia"),
            ("MNP", "Northern Mariana Islands"), ("NOR", "Norway"), ("OMN", "Oman"), ("PAK", "Pakistan"), ("PLW", "Palau"),
            ("PSE", "Palestine, State of"), ("PAN", "Panama"), ("PNG", "Papua New Guinea"), ("PRY", "Paraguay"), ("PER", "Peru"),
            ("PHL", "Philippines"), ("PCN", "Pitcairn"), ("POL", "Poland"), ("PRT", "Portugal"), ("PRI", "Puerto Rico"),
            ("QAT", "Qatar"), ("REU", "Réunion"), ("ROU", "Romania"), ("RUS", "Russian Federation"), ("RWA", "Rwanda"),
            ("BLM", "Saint Barthélemy"), ("SHN", "Saint Helena, Ascension and Tristan da Cunha"), ("KNA", "Saint Kitts and Nevis"), ("LCA", "Saint Lucia"), ("MAF", "Saint Martin (French part)"),
            ("SPM", "Saint Pierre and Miquelon"), ("VCT", "Saint Vincent and the Grenadines"), ("WSM", "Samoa"), ("SMR", "San Marino"), ("STP", "Sao Tome and Principe"),
            ("SAU", "Saudi Arabia"), ("SEN", "Senegal"), ("SRB", "Serbia"), ("SYC", "Seychelles"), ("SLE", "Sierra Leone"),
            ("SGP", "Singapore"), ("SXM", "Sint Maarten (Dutch part)"), ("SVK", "Slovakia"), ("SVN", "Slovenia"), ("SLB", "Solomon Islands"),
            ("SOM", "Somalia"), ("ZAF", "South Africa"), ("SGS", "South Georgia and the South Sandwich Islands"), ("SSD", "South Sudan"), ("ESP", "Spain"),
            ("LKA", "Sri Lanka"), ("SDN", "Sudan"), ("SUR", "Suriname"), ("SJM", "Svalbard and Jan Mayen"), ("SWE", "Sweden"),
            ("CHE", "Switzerland"), ("SYR", "Syrian Arab Republic"), ("TWN", "Taiwan"), ("TJK", "Tajikistan"), ("TZA", "Tanzania"),
            ("THA", "Thailand"), ("TLS", "Timor-Leste"), ("TGO", "Togo"), ("TKL", "Tokelau"), ("TON", "Tonga"),
            ("TTO", "Trinidad and Tobago"), ("TUN", "Tunisia"), ("TUR", "Türkiye"), ("TKM", "Turkmenistan"), ("TCA", "Turks and Caicos Islands"),
            ("TUV", "Tuvalu"), ("UGA", "Uganda"), ("UKR", "Ukraine"), ("ARE", "United Arab Emirates"), ("GBR", "United Kingdom"),
            ("USA", "United States of America"), ("UMI", "United States Minor Outlying Islands"), ("URY", "Uruguay"), ("UZB", "Uzbekistan"), ("VUT", "Vanuatu"),
            ("VEN", "Venezuela"), ("VNM", "Viet Nam"), ("VGB", "Virgin Islands (British)"), ("VIR", "Virgin Islands (U.S.)"), ("WLF", "Wallis and Futuna"),
            ("ESH", "Western Sahara"), ("YEM", "Yemen"), ("ZMB", "Zambia"), ("ZWE", "Zimbabwe"),
        ];
        return list.OrderBy(entry => entry.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("en-GB"), ignoreCase: true))
            .ToDictionary(entry => entry.Code, entry => entry.Name, StringComparer.Ordinal);
    }
}
