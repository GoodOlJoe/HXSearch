<#

DSP Interconnection Rules

We're starting with a populated Preset object which has two Dsps, and each Dsp
has up to four lists of blocks, each representing the S, A, B, and J portions of
that SINGLE Dsp's block structure, a.k.a. the "SABJ block lists". Any block list
can be null, including all of them being null if there are no blocks on that
dsp. The arrangment of individual blocks into the SABJ block lists is according
to the topology of the DSP (the topology being one of A, AB, SABJ, SAB, ABJ).
But that arrangement has already taken place in other code before we get to DSP
Inteconnection Rules.

IMPORTANTLY block list segments must end with an output block if they do in fact
end at an output. This could be any A, B, or J segments. In all topologies, at
least one segment ends in an output block, and it's never an S segment. That
output block must be represented in some way, presuambly by an actual block of
type output at the end of the segment, with properties indicating where it's
output to, which coudld be one of (logically) T, iA, iB, iA+B.

The ultimate goal is to have a single "signature" for each each EFFECTIVE signal
path in the preset, where that signature contains information from which we can
derive:
    * all blocks in the chain including the model name and the category of block
      (e.g., DELAY, REVERB)
    * the sequence of those blocks
    * up to two parallel signal chain portions

The idea is to be able to find presets containing patterns based on
    * presence or absence of one or more blocks, wtih the ability to specify
      both block type and model name for each block (find a preset that has both
      an Opti Trem and an amp)
    * sequence of any number of blocks (e.g., "find a preset with two
      compressors that have an EQ between them")
    * parallelism ("find a preset with any amp running in parallel with the AC
      Fawn 30")

The question is how to connect the SABJ block lists from Dsp0 with those from
Dsp1 in order to form the complete EFFECTIVE signal path or paths contained in
that preset.

The methods used to connect the SABJ block lists from Dsp0 to Dsp1 are
determined by two factors
    * FIRST the configuration of Dsp0 output blocks and Dsp1 input blocks
    * SECOND depending on the O/I configuration, the topology (SABJ) of the signal chain on each dsp

Dsp0 has two outputs, and for our purposes each of them can be assigned one of four targets:
    * Terminal output : this is any physical jack leaving the Helix device,
      meaning it is not targeted at any output of dsp1. This is represented as T
      in the output codes below. In the Helix preset JSON this is any value
      other than 2
    * Path 2A: this is dsp0 Input A, represented by 2 in the helix preset and iA in the output codes below
    * Path 2B: this is dsp0 Input B, represented by 3 in the helix preset and iB in the output codes below
    * Path 2A+B: this is dsp0 Input A+B, represented by 4 in the helix preset and iAB in the output codes below


==========================================
==========================================
RULES

    ESC = Effective Signal Chain
    NESC = Next Effective Signal Chain in the list for this preset
==========================================
==========================================

switch ( dsp0 output A )

    terminal : for


#>
$topo0 = ("A-0", "AB-0", "SABJ-0", "SAB-0", "ABJ-0")
$topo1 = ("A-1", "AB-1", "SABJ-1", "SAB-1", "ABJ-1")

# the format is o{ A | B }-t{ T | iA | iB | iAB }
# where
#   * o means an output on dsp0
#   * A|B refers to which output: A, or B
#   * - can be read as "routes to"
#   * t can be read as "target"
#   * T|iA|iB-iAB names the target
#       * T - terminal (any physical output jack, not leading to dsp1)
#       * iA - dsp1 input 1A
#       * iB - dsp1 input 1B
#       * iAB - both dsp1 input 1A AND dsp1 inputB

$outA = (
    @{ label = "dsp 0 output A: term"; id = "oA-tT"; col = 0; },
    @{ label = "dsp 0 output A: dsp 1 input A"; id = "oA-tiA"; col = 1;},
    @{ label = "dsp 0 output A: dsp 1 input B"; id = "oA-tiB"; col = 2; },
    @{ label = "dsp 0 output A: dsp 1 input A+B"; id = "oA-tiAB"; col = 3; }
)
$outB = (
    @{ label = "dsp 0 output B: term"; id = "oBit"; row = 0; },
    @{ label = "dsp 0 output B: dsp 1 input A"; id = "oBiA"; row = 1; },
    @{ label = "dsp 0 output B: dsp 1 input B"; id = "oBiB"; row = 2; },
    @{ label = "dsp 0 output B: dsp 1 input A+B"; id = "oBiAB"; row = 3; }
)

$interconnectCount = 1
$grossTopologyCount = 1
$outA | ForEach-Object {
    $thisOutA = $_
    $outB | ForEach-Object {
        $thisOutB = $_
        $interconnectKey = "$($thisOutA.id)`n$($thisOutB.id)"
        Write-Host "`n", $interconnectCount
        Write-Host $interconnectKey
        Write-Host $thisOutA.label, "`n", $thisOutB.label
        $interconnectCount += 1
        $topo0 | ForEach-Object {
            $thisTopo0 = $_
            $topo1 | ForEach-Object {
                $thisTopo1 = $_
                if ($thisTopo0.EndsWith("AB-0") -and $thisTopo1.StartsWith("AB")) {
                    Write-Host "    $grossTopologyCount $thisTopo0 $thisTopo1"
                    $grossTopologyCount += 1
                }
            }
        }
    }
}