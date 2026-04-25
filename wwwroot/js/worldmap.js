// worldmap.js — World Map page logic
// Depends on: D3.js v7 and TopoJSON (both loaded via ViewData["ExtraHead"] in Worldmap.cshtml)
// Data global injected by the @section Scripts block in Worldmap.cshtml:
//   countryData — object mapping ISO alpha-2 codes to play counts { "US": 142, "GB": 87, ... }

// ── Lookup tables ──

// Maps numeric ISO 3166-1 country codes (used by TopoJSON) → alpha-2 codes (used in countryData)
const numToAlpha2 = {
    840:"US",124:"CA",826:"GB",250:"FR",276:"DE",36:"AU",392:"JP",
    76:"BR",566:"NG",710:"ZA",752:"SE",578:"NO",528:"NL",724:"ES",
    380:"IT",484:"MX",32:"AR",170:"CO",356:"IN",156:"CN",410:"KR",
    643:"RU",818:"EG",288:"GH",388:"JM",372:"IE",554:"NZ",208:"DK",
    756:"CH",56:"BE",620:"PT",616:"PL",804:"UA",792:"TR",360:"ID",
    608:"PH",458:"MY",702:"SG",764:"TH",704:"VN",586:"PK",50:"BD",
    144:"LK",404:"KE",231:"ET",834:"TZ",800:"UG",384:"CI",686:"SN",
    120:"CM",24:"AO",780:"TT",192:"CU",214:"DO",591:"PA",188:"CR",
    320:"GT",340:"HN",222:"SV",558:"NI",68:"BO",600:"PY",858:"UY",
    862:"VE",218:"EC",604:"PE",152:"CL",300:"GR",348:"HU",203:"CZ",
    703:"SK",40:"AT",246:"FI",233:"EE",428:"LV",440:"LT",100:"BG",
    642:"RO",191:"HR",688:"RS",705:"SI",807:"MK",70:"BA",499:"ME",
    8:"AL",112:"BY",398:"KZ",860:"UZ",634:"QA",784:"AE",682:"SA",
    368:"IQ",364:"IR",376:"IL",400:"JO",422:"LB",760:"SY",504:"MA",
    788:"TN",12:"DZ",434:"LY",466:"ML",854:"BF",562:"NE",706:"SO",
    266:"GA",178:"CG",180:"CD",646:"RW",108:"BI",454:"MW",894:"ZM",
    716:"ZW",508:"MZ",72:"BW",516:"NA",748:"SZ",426:"LS"
};

// Maps alpha-2 codes → display names for the tooltip
const alpha2ToName = {
    "US":"United States","CA":"Canada","GB":"United Kingdom","FR":"France",
    "DE":"Germany","AU":"Australia","JP":"Japan","BR":"Brazil","NG":"Nigeria",
    "ZA":"South Africa","SE":"Sweden","NO":"Norway","NL":"Netherlands",
    "ES":"Spain","IT":"Italy","MX":"Mexico","AR":"Argentina","CO":"Colombia",
    "IN":"India","CN":"China","KR":"South Korea","RU":"Russia","EG":"Egypt",
    "GH":"Ghana","JM":"Jamaica","IE":"Ireland","NZ":"New Zealand","DK":"Denmark",
    "CH":"Switzerland","BE":"Belgium","PT":"Portugal","PL":"Poland","UA":"Ukraine",
    "TR":"Turkey","ID":"Indonesia","PH":"Philippines","MY":"Malaysia",
    "SG":"Singapore","TH":"Thailand","VN":"Vietnam","PK":"Pakistan",
    "BD":"Bangladesh","LK":"Sri Lanka","KE":"Kenya","ET":"Ethiopia",
    "TZ":"Tanzania","UG":"Uganda","CI":"Ivory Coast","SN":"Senegal",
    "CM":"Cameroon","AO":"Angola","TT":"Trinidad & Tobago","CU":"Cuba",
    "DO":"Dominican Republic","PA":"Panama","CR":"Costa Rica","GT":"Guatemala",
    "HN":"Honduras","SV":"El Salvador","NI":"Nicaragua","BO":"Bolivia",
    "PY":"Paraguay","UY":"Uruguay","VE":"Venezuela","EC":"Ecuador",
    "PE":"Peru","CL":"Chile","GR":"Greece","HU":"Hungary","CZ":"Czech Republic",
    "SK":"Slovakia","AT":"Austria","FI":"Finland","EE":"Estonia","LV":"Latvia",
    "LT":"Lithuania","BG":"Bulgaria","RO":"Romania","HR":"Croatia","RS":"Serbia",
    "SI":"Slovenia","MK":"N. Macedonia","BA":"Bosnia","ME":"Montenegro",
    "AL":"Albania","BY":"Belarus","KZ":"Kazakhstan","UZ":"Uzbekistan",
    "QA":"Qatar","AE":"UAE","SA":"Saudi Arabia","IQ":"Iraq","IR":"Iran",
    "IL":"Israel","JO":"Jordan","LB":"Lebanon","SY":"Syria","MA":"Morocco",
    "TN":"Tunisia","DZ":"Algeria","LY":"Libya","ML":"Mali","BF":"Burkina Faso",
    "NE":"Niger","GA":"Gabon","CG":"Congo","CD":"DR Congo","RW":"Rwanda",
    "BI":"Burundi","MW":"Malawi","ZM":"Zambia","ZW":"Zimbabwe","MZ":"Mozambique",
    "BW":"Botswana","NA":"Namibia","SZ":"Eswatini","LS":"Lesotho","SO":"Somalia"
};

// The highest play count among all countries — used to normalise colour intensity
const maxCount = Object.values(countryData).reduce((a, b) => Math.max(a, b), 1);

// ── Colour scale ──
// Maps play count → RGB colour between dark teal (few plays) and Spotify green (many plays).
// Uses a power curve (^0.4) so mid-range countries aren't all washed out.
function getColor(plays) {
    if (!plays) return null; // no data — use CSS default fill
    const t = Math.pow(plays / maxCount, 0.4);
    const r = Math.round(13  + t * (29  - 13));
    const g = Math.round(42  + t * (185 - 42));
    const b = Math.round(20  + t * (84  - 20));
    return `rgb(${r},${g},${b})`;
}

// ── Main map initialiser ──
// Called on DOMContentLoaded. Sets up the D3 projection, zoom behaviour,
// loads TopoJSON from CDN, draws countries, and wires up tooltip + zoom buttons.
function initMap() {
    const mapArea = document.querySelector('.map-area');
    const svg = d3.select('#world-map-svg');

    function getDims() { return { w: mapArea.clientWidth, h: mapArea.clientHeight }; }
    let { w, h } = getDims();

    // Natural Earth projection — looks good at world scale
    const projection = d3.geoNaturalEarth1().scale(w / 6.2).translate([w / 2, h / 2]);
    const path = d3.geoPath().projection(projection);

    // D3 zoom — allows drag-pan and scroll-to-zoom, clamped to 0.7×–14×
    const zoom = d3.zoom().scaleExtent([0.7, 14]).on('zoom', e => mapGroup.attr('transform', e.transform));
    svg.call(zoom).on('dblclick.zoom', null); // disable double-click zoom (conflicts with UX)

    // Dark ocean background
    svg.append('rect').attr('width', '100%').attr('height', '100%').attr('fill', '#080c12');

    const mapGroup = svg.append('g'); // all map paths go inside this group so zoom transforms them together

    // Graticule lines (latitude / longitude grid)
    mapGroup.append('path').datum(d3.geoGraticule()()).attr('class', 'graticule').attr('d', path);

    const tooltip  = document.getElementById('map-tooltip');
    const ttName   = tooltip.querySelector('.tt-name');
    const ttPlays  = tooltip.querySelector('.tt-plays');

    // ── Load country geometry from CDN ──
    async function loadMap() {
        const res   = await fetch('https://cdn.jsdelivr.net/npm/world-atlas@2/countries-50m.json');
        const world = await res.json();
        const geojson = topojson.feature(world, world.objects.countries);

        // Draw one <path> per country
        mapGroup.selectAll('.country')
            .data(geojson.features)
            .enter().append('path')
            .attr('class', 'country')
            .attr('d', path)
            .attr('fill', d => {
                const iso   = numToAlpha2[parseInt(d.id, 10)];
                const plays = iso ? (countryData[iso] || 0) : 0;
                return getColor(plays) || '#111e2e'; // fallback for countries with no data
            })
            .on('mousemove', function(event, d) {
                const iso   = numToAlpha2[parseInt(d.id, 10)];
                const plays = iso ? (countryData[iso] || 0) : 0;
                if (!plays) { tooltip.style.display = 'none'; return; } // no data — hide tooltip

                const name = (iso && alpha2ToName[iso]) ? alpha2ToName[iso] : (iso || 'Unknown');
                ttName.textContent  = name;
                ttPlays.textContent = plays.toLocaleString() + ' plays';

                // Position tooltip near the cursor but keep it inside the map area
                const rect = mapArea.getBoundingClientRect();
                let tx = event.clientX - rect.left + 16;
                let ty = event.clientY - rect.top  - 12;
                if (tx + 170 > rect.width)  tx = event.clientX - rect.left - 170;
                if (ty + 60  > rect.height) ty = event.clientY - rect.top  - 60;
                tooltip.style.left    = tx + 'px';
                tooltip.style.top     = ty + 'px';
                tooltip.style.display = 'block';
            })
            .on('mouseleave', () => { tooltip.style.display = 'none'; });

        // Country border mesh — drawn on top of fill paths so borders are always visible
        mapGroup.append('path')
            .datum(topojson.mesh(world, world.objects.countries, (a, b) => a !== b))
            .attr('class', 'country-border')
            .attr('d', path);
    }

    loadMap().catch(e => console.error('Map load error:', e));

    // ── Zoom buttons ──
    document.getElementById('zoom-in').addEventListener('click',
        () => svg.transition().duration(300).call(zoom.scaleBy, 1.5));
    document.getElementById('zoom-out').addEventListener('click',
        () => svg.transition().duration(300).call(zoom.scaleBy, 0.67));
    document.getElementById('zoom-reset').addEventListener('click',
        () => svg.transition().duration(400).call(zoom.transform, d3.zoomIdentity));

    // ── Resize handler ──
    // Recalculates projection when the window is resized so the map stays centred
    window.addEventListener('resize', () => {
        const { w: nw, h: nh } = getDims();
        projection.scale(nw / 6.2).translate([nw / 2, nh / 2]);
        mapGroup.selectAll('path').attr('d', path);
    });
}

// ── Entry point ──
document.addEventListener('DOMContentLoaded', initMap);
