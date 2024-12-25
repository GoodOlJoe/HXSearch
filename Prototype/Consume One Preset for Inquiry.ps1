using namespace System.Text
using namespace System.Collections.Generic


# for each of the two DPSs, create a list of s, a, b, and j blocks according to the topology of the dsp
# now consider four blocks lists and two DSps, we can call them s0, a0, b0, j0, s1, a1, b1, j1

# either path on dsp0 can go to a physical output OR to either input on dsp1, so
# the complete permutation set for signal paths is

# (output codes are 1-12 in order listed in the HX Edit inspector, from Multi Output to USB 5/6)
# so 2, 3, and 4 are input to dsp1, anything else i sa physical output
# an output of 0 means it's there's no output for that path (feeds back into a join)


# generate Signatures, again according to the topology. sig0 is the signature of path
#   A
#     sig0 = a
#     sig1 = 
#     sigP = 
#   AB
#   SABJ
#   SAB
#   ABJ

# Each preset can have up to four signal chains, and each chain can have up to those three signatures.
# If a chain exists, it will have at least sigA. SigB and sigPara will be null if the chain has no splits.

# Preset
#   chain 1
#       sigA
#       sigB
#       sigPara
#   chain 2
#       sigA
#       sigB
#       sigPara
#   chain 3
#       sigA
#       sigB
#       sigPara
#   chain 4
#       sigA
#       sigB
#       sigPara

# #head
# ~hcEQ ~hdSimple EQ ~hcAmp ~hdWhoWatt100 

# #parallel path A
# ~acCab ~ad1x12 US Deluxe ~acSynth ~adSimple Pitch 

# #parallel path B
# ~bcReverb ~bdDynamic Hall ~bcEQ ~bdLow and High Cut 

# #tails
# ~tcMod ~tdOptical Trem 

# # Head ParallelA Tail
# ~hcEQ ~hdSimple EQ ~hcAmp ~hdWhoWatt100 ~acCab ~ad1x12 US Deluxe ~acSynth ~adSimple Pitch ~tcMod ~tdOptical Trem 

# # Head ParallelB Tail
# ~hcEQ ~hdSimple EQ ~hcAmp ~hdWhoWatt100 ~bcReverb ~bdDynamic Hall ~bcEQ ~bdLow and High Cut ~tcMod ~tdOptical Trem 

# # Parallel Portions
# ~acCab ~ad1x12 US Deluxe ~acSynth ~adSimple Pitch ~bcReverb ~bdDynamic Hall ~bcEQ ~bdLow and High Cut 


# # search only Path A and Path B signatures
# # match a signature that has any EQ before any Cab
# ~[sabj]cEQ.*~[sabj]cCab

# # search only Path A and Path B signatures
# # match a signature that has any Modulation block
# ~[sabj]cMod

# # search only Path A and Path B signatures
# # match a signature that uses the Optical Trem block
# ~[sabj]dOptical Trem

# # search only Parallel portion signatures
# # match a signature that has any Cab running in parallel with any Reverb
# ~acCab.*~bcReverb

# # search only Parallel portion signatures
# # match a signature that has any two Amps running in parallel
# ~acAmp.*~bcAmp

# # search only Parallel portion signatures
# # match a signature that has the A30 Fawn Nrm running in parallel with any other amp
# (~acAmp.*~bdA30 Fawn Nrm)|(~adA30 Fawn Nrm.*~bcAmp)

<#

data.tone.global.topology{dspNum}
(M) represents a module (block)
[a] is the portion of the path stored in .a
[b] is the portion of the path stored in .b
[s] is the portion of the path stored in .s (i.e., before the Split)
[s] is the portion of the path stored in .j (i.e., after the Join)

A ===================================================================

   v---- [a] ---------v
>--(M)--+--(M)--+--(M)-->

AB ==================================================================

   v---- [a] ----------v
>--(M)--+--(M)--+--(M)-->
>--(M)--+--(M)--+--(M)-->
   ^---- [b] --------^

SABJ ================================================================
   v-- [s] ----v  v--- [a] ----v v-- [j] ------v
>------(M)------+------(M)------+------(M)------>
                |               |
                +------(M)------+
                 ^---- [b] ----^
SAB =================================================================
   v-- [s] ----v v-- [a] ---------------------v
>------(M)------+------(M)------+------(M)------>
                |
                +------(M)------+------(M)------>
                 ^-- [b] ---------------------^

ABJ =================================================================
   v-- [a] --------------------v v-- [j] ----v
>------(M)------+------(M)------+------(M)------>
                                |
>------(M)------+------(M)------+
   ^-- [b] -------------------^

#>
function DumpAggregateChains {

    param (
        [Preset]$p,
        [switch]$ShowConnections
    )

    $indentLevel = 1
    $indent = "    "

    Write-Host 
    Write-Host "Preset display name: $($p.Name)"
    Write-Host "Preset file:         $($p.FQN)"
    Write-Host "Topology:            $($p.dsp[0].topology)  $($p.dsp[1].topology)"

    [int]$splitDepth = 0
    (0..1) | ForEach-Object {
        $dspNum = $_
        (0..1) | ForEach-Object {
            $inputNum = $_
            if ( $p.aggregateChain[$dspNum, $inputNum]) {
                [Block] $blk = $p.aggregateChain[$dspNum, $inputNum]
                
                # All input blocks are included in aggregateChains whether they
                # originate from an external input jack or not. But we only want
                # to SEE those that originate from an external jack because if
                # they don't, they're already going to be included in another
                # signal chain (one that feeds its output into this virtual
                # input). Only non-zero inputs map to external physical inputs.

                if ($blk -is [InputBlock] -and 0 -ne $blk.structure."@input") {

                    [Block] $pathBHead = $null;
                    Write-Host "`n=== dsp$dspNum input$inputNum ==================================================`n"
                    while ($blk) {

                        [string]$inSplit = ""
                        # if ( $blk.precedingSplit ) {
                        #     [string]$inSplit = "(in split $($blk.precedingSplit.id))"
                        # }

                        if ($blk -is [SplitBlock]) {
                            Write-Host ($indent * $indentLevel), "parallel (", "[$($blk.id)]"
                            $pathBHead = $blk.NextR # we will need to come back to display this side
                            $indentLevel = $indentLevel + 1
                            $splitDepth = $splitDepth + 1
                            $blk = $blk.NextL
                        }
                        elseif ($blk -is [JoinBlock] -and $pathBHead ) {
                            # there is a path to backtrack to (because we're in a split)
                            Write-Host ($indent * $indentLevel), "--- and ---", $inSplit
                            $blk = $pathBHead
                            $pathBHead = $null
                        }
                        elseif ($blk -is [JoinBlock] ) {
                            # there is no path to backtrack to

                            if ( $splitDepth -gt 0) {
                                # we're exiting a parallel segment
                                $indentLevel = $indentLevel - 1
                                Write-Host ($indent * $indentLevel), ")", $inSplit
                                $splitDepth = $splitDepth - 1
                            }

                            # else we're not in a split, it's just a join from another
                            # path coming. It doesn't affect the signal path we're
                            # currently on...nothing to display, just proceed

                            $blk = $blk.nextL
                        }
                        elseif ($blk -is [ImpliedBlock] ) {
                            # Write-Host ($indent * $indentLevel), "Implied Output", $inSplit
                            $blk = $blk.nextL
                        }
                        else {
                            if ( $showConnections.IsPresent -or !($blk -is [InputBlock] -or $blk -is [OutputBlock]) ) {
                                Write-Host ($indent * $indentLevel), $blk.structure."@model", $inSplit
                            }
                            $blk = $blk.nextL
                        }
                    }
                }
            }
        }
    }

    while ( $splitDepth -gt 0) {
        
        # We ended with an unclosed split. I think this can only happen in a
        # preset where dsp0 splits, never rejoins, and dsp0.outputA goes to one
        # input of dsp1 while dsp0.outputB goes to both dsp1 inputs. The aggregate
        # chain correct represents the parallelism but when we create the
        # signature or dump for debug we need to close the unclosed split. Could
        # probably figure this out algorithmically instead of doing this when we
        # generate signatures. It's just such a weird case.

        $indentLevel = $indentLevel - 1
        Write-Host ($indent * $indentLevel), ") HACK"
        $splitDepth = $splitDepth - 1
    }

    Write-Host 
}
function DumpLPath([Block]$head) {
    [StringBuilder]$sb = [StringBuilder]::new(30)
    while ($head) {
        $sb.AppendLine("$($head.GetType().Name)-$($head.structure."@model") ($($head.id)) [Split $($head.precedingSplit.id)]")
        $head = $head.nextL
    }
    Write-Host $sb.ToString()
}
function DumpRPath([Block]$head) {
    [StringBuilder]$sb = [StringBuilder]::new(30)
    while ($head) {
        $sb.AppendLine("$($head.GetType().Name)-$($head.structure."@model")  ($($head.id)) [Split $($head.precedingSplit.id)]")
        if ( $head.nextR) { $head = $head.nextR }
        else { $head = $head.nextL }
    }
    Write-Host $sb.ToString()
}
class Block {
    [string]$id
    # [Int64]$travID # traversal ID
    [int] $dspnum
    [int] $blocknum
    [int] $path
    [int] $position
    [int] $category # e.g., AMP, DELAY, REVERB, OUTPUT (or TAP?)
    [System.Object] $structure # dynamically created from the json block
    [Block] $precedingSplit = $null# most recent split before this block, or $null if not on a parallel path
    [Block] $nextL
    [Block] $nextR

    Block([int] $dspnum , [int] $blocknum , [int] $path , [int] $position , [System.Object] $structure) {
        $this.id = $global:blockInstanceID
        $global:blockInstanceID = $global:blockInstanceID + 1
        $this.dspnum = $dspnum
        $this.blocknum = $blocknum
        $this.path = $path
        $this.position = $position
        $this.structure = $structure
    }
    [string]Signature() { return " m$($this.structure."@model")" }
    [List[int]]upstreamSplitIds() {
        if (!$this.precedingSplit) {
            return $null
        }
        else {
            [List[int]]$upstream = [List[int]]::new(5)
            [Block]$b = $this
            while ($b.precedingSplit) {
                $upstream.Add($b.precedingSplit.id)
                $b = $b.precedingSplit
            }
            return $upstream
        }
    }
    [Block]Clone([Block]$b) {
        $b.nextL = $this.nextL
        $b.nextR = $this.nextR
        $b.precedingSplit = $this.precedingSplit
        return $b
    }
}
class InputBlock : Block {
    [string] $whichInput
    InputBlock(
        [int] $dspnum,
        [int] $blocknum,
        [int] $path,
        [int] $position,
        [string]$whichInput,
        [System.Object] $structure 
    ) : base( $dspnum, $blocknum, $path, $position, $structure ) {
        $this.whichInput = $whichInput
    }
    [string]Signature() { return " $($this.structure."@input")->m$($this.structure."@model")" }
    [InputBlock]Clone() {
        return ([Block]$this).Clone([InputBlock]::new(
                $this.dspnum,
                $this.blocknum,
                $this.path,
                $this.position,
                $this.whichInput,
                $this.structure 
            )
        ) 
    }
}
class SplitBlock : Block {
    SplitBlock( [int] $dspnum ) : base( $dspnum, 0, 0, 0, $structure ) 
    {}
    [SplitBlock]Clone() {
        return ([Block]$this).Clone([SplitBlock]::new(
                $this.dspnum, $this.blocknum, $this.path, $this.position, $this.structure 
            )
        ) 
    }
}
class JoinBlock : Block {
    JoinBlock( [int] $dspnum ) : base( $dspnum, 0, 0, 0, $structure ) 
    {}
    [JoinBlock]Clone() {
        return ([Block]$this).Clone([JoinBlock]::new(
                $this.dspnum, $this.blocknum, $this.path, $this.position, $this.structure 
            )
        ) 
    }
}
class ImpliedBlock : Block {
    ImpliedBlock() : base( 0, 0, 0, 0, $structure ) 
    {}
    [ImpliedBlock]Clone() {
        return ([Block]$this).Clone([ImpliedBlock]::new(
                $this.dspnum, $this.blocknum, $this.path, $this.position, $this.structure 
            )
        ) 
    }
}
class OutputBlock : Block {
    [string] $whichOutput
    
    # $true if this output terminates a non-merged split path, e.g., ends of an
    # SAB topology
    # [bool] $parallelAtOutput 
    
    OutputBlock(
        [int] $dspnum,
        [int] $blocknum,
        [int] $path,
        [int] $position,
        [string]$whichOutput,
        [System.Object] $structure 
    ) : base( $dspnum, $blocknum, $path, $position, $structure ) {
        $this.whichOutput = $whichOutput
    }
    [string]Signature() { return " m$($this.structure."@model")->$($this.structure."@output")" }
    [OutputBlock]Clone() {
        return ([Block]$this).Clone([OutputBlock]::new(
                $this.dspnum,
                $this.blocknum,
                $this.path,
                $this.position,
                $this.whichOutput,
                $this.structure 
            )
        ) 
    }
}
class Dsp {
    [Block[]] $a
    [Block[]] $b
    [Block[]] $s
    [Block[]] $j
    [string] $topology
    
    # input[n] will point to the first block in a linked list of blocks
    # representing the signal chain for inputs A and B. Where n=0 for inputA and
    # n=1 for inputB
    [Block[]] $input = [Block[]]::new(2)
    
    [Hashtable] $signatures = @{"A" = @(); "B" = @() } # probably will not be used when I'm done

    Dsp( 
        [InputBlock[]] $inputBlocks,
        [OutputBlock[]] $outputBlocks,
        [Block[]] $blocks,
        [string]$topology,
        [int]$dspNum,
        [int] $splitPos,
        [int] $joinPos 
    ) {
        # Construct linked lists of blocks representing the signal flow from each
        # input A and B, if they exist. We do this in two steps. First we
        # construct simple lists of blocks representing the S, A, B, and J
        # segments according to the DSP topology. Second we assemble those
        # segments into linked list structures, inserting explicit Split and Join
        # blocks where needed.

        $this.topology = $topology

        switch ( $topology.ToUpper()) {
            "A" {
                $this.a += $this.GetInput( $inputBlocks, $dspNum, "A") 
                $this.a += $blocks | Where-Object { $_.dspNum -eq $dspNum }  | Sort-Object -Property position
                $this.a += $this.GetOutput( $outputBlocks, $dspNum, "A")
                # $this.a[-1].parallelAtOutput = $false
                if ($this.a[0] -is [InputBlock]) {
                    $this.input[0] = $this.LinkedBlockList($this.a)
                }
            }
            "AB" {
                $this.a += $this.GetInput( $inputBlocks, $dspNum, "A")
                $this.b += $this.GetInput( $inputBlocks, $dspNum, "B")
                
                $this.a += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 0 -eq $_.path } | Sort-Object -Property position
                $this.b += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 1 -eq $_.path } | Sort-Object -Property position
                
                $this.a += $this.GetOutput( $outputBlocks, $dspNum, "A")
                $this.b += $this.GetOutput( $outputBlocks, $dspNum, "B")

                # $this.a[-1].parallelAtOutput = $false
                # $this.b[-1].parallelAtOutput = $false

                if ($this.a[0] -is [InputBlock]) { $this.input[0] = $this.LinkedBlockList($this.a) }
                if ($this.b[0] -is [InputBlock]) { $this.input[1] = $this.LinkedBlockList($this.b) }
            }
            "SABJ" {
                $this.s += $this.GetInput( $inputBlocks, $dspNum, "A")

                $this.s += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 0 -eq $_.path -and $_.position -lt $splitPos } | Sort-Object -Property position
                $this.a += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 0 -eq $_.path -and $_.position -ge $splitPos -and $_.position -lt $joinPos } | Sort-Object -Property position
                $this.b += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 1 -eq $_.path } | Sort-Object -Property position
                $this.j += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 0 -eq $_.path -and $_.position -ge $joinPos } | Sort-Object -Property position
                
                $this.j += $this.GetOutput( $outputBlocks, $dspNum, "A")
                # $this.j[-1].parallelAtOutput = $false

                #create the Split
                [Block]$sBlock = [SplitBlock]::new($dspNum)

                # if there were no s segment modules we still need to represent
                # the split so allocate the s segment here and add the split to it

                if (!$this.s) {
                    $this.s = [Block[]]::new()
                    $this.s += $sBlock
                }

                # on dsp1 we could have SABJ with no S, no A, AND the s segment
                # would not being with an input because we only add external
                # inputs. if dsp1 is SABJ but without modules on S segment and
                # with input coming from dsp0 output (instead of an external
                # input) then we will have no s segment at all

                [Block]$jBlock = [JoinBlock]::new($dspNum)
                $jBlock.precedingSplit = $sBlock
                $jBlock.nextL = $this.LinkedBlockList($this.j)
                if ( $this.a ) {
                    # SABJ topology can have an empty A segment
                    $this.a[-1].nextL = $jBlock 
                    $this.a | ForEach-Object { $_.precedingSplit = $sBlock } # point every A module back to the split
                } 

                $this.b[-1].nextL = $jBlock
                $this.b | ForEach-Object { $_.precedingSplit = $sBlock } # point every A module back to the split

                if ( $this.a ) {
                    $sBlock.nextL = $this.LinkedBlockList($this.a)
                }
                else {
                    $sBlock.nextL = $jBlock  # if no a segment, the left path fo s connects direct to the join
                }
                $sBlock.nextR = $this.LinkedBlockList($this.b)
                $this.s[-1].nextL = $sBlock

                $this.input[0] = $this.LinkedBlockList($this.s) 
            }
            "SAB" {
                $this.s += $this.GetInput( $inputBlocks, $dspNum, "A")

                $this.s += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 0 -eq $_.path -and $_.position -lt $splitPos } | Sort-Object -Property position
                $this.a += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 0 -eq $_.path -and $_.position -ge $splitPos } | Sort-Object -Property position
                $this.b += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 1 -eq $_.path } | Sort-Object -Property position

                $this.a += $this.GetOutput( $outputBlocks, $dspNum, "A")
                $this.b += $this.GetOutput( $outputBlocks, $dspNum, "B")

                # $this.a[-1].parallelAtOutput = $true
                # $this.b[-1].parallelAtOutput = $true

                # if there were no s segment modules we still need to represent
                # the split so allocate the s segment here

                [Block]$sBlock = [SplitBlock]::new($dspNum)
                if (!$this.s) {
                    $this.s = [Block[]]::new()
                }

                $sBlock.nextL = $this.LinkedBlockList($this.a)
                $sBlock.nextR = $this.LinkedBlockList($this.b)
                $this.s[-1].nextL = $sBlock

                $this.input[0] = $this.LinkedBlockList($this.s) 

                $this.a | ForEach-Object { $_.precedingSplit = $sBlock } # point every A module back to the split
                $this.b | ForEach-Object { $_.precedingSplit = $sBlock } # point every A module back to the split
            }
            "ABJ" {
                $this.a += $this.GetInput( $inputBlocks, $dspNum, "A")
                $this.b += $this.GetInput( $inputBlocks, $dspNum, "B")

                $this.a += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 0 -eq $_.path -and $_.position -lt $joinPos } | Sort-Object -Property position
                $this.b += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 1 -eq $_.path } | Sort-Object -Property position
                $this.j += $blocks | Where-Object { $_.dspNum -eq $dspNum -and 0 -eq $_.path -and $_.position -ge $joinPos } | Sort-Object -Property position

                $this.j += $this.GetOutput( $outputBlocks, $dspNum, "A")
                # $this.j[-1].parallelAtOutput = $false

                if ($this.a[0] -is [InputBlock] -and $this.b[0] -is [InputBlock]) {

                    [Block]$jBlock = [JoinBlock]::new($dspNum)
                    $jBlock.nextL = $this.LinkedBlockList($this.j)
                    $this.a[-1].nextL = $jBlock
                    $this.b[-1].nextL = $jBlock

                    $this.input[0] = $this.LinkedBlockList($this.a) 
                    $this.input[1] = $this.LinkedBlockList($this.b) 
                }

            }
        }
    }
    [void]Dump([int]$dspNum) {
        # ($this.s, $this.a, $this.b, $this.j) |
        ("s", "a", "b", "j") |
        ForEach-Object {
            Write-Host "`ndsp[$($dspNum)].$_  $($this.topology)`n=========="
            if ($this."$_") {
                $this."$_" |
                ForEach-Object {
                    Write-Host $_.GetType().Name, $_.id, $_.structure."@model"
                }
            }
        }
        Write-Host 
    }
    hidden [bool]IsTerminal([OutputBlock]$b) { return $b.structure."$@output" -lt 2 -or $b.structure."$@output" -gt 4 }
    hidden [Block]LinkedBlockList([Block[]] $blockArray ) {
        if (!$blockArray -or 0 -eq $blockArray.Count) { return $null }

        [int] $i = 0
        while ( $i -le $blockArray.Count - 2 ) {
            $blockArray[$i].nextL = $blockArray[$i + 1]
            $i = $i + 1
        }
        return $blockArray[0];
    }
    hidden [OutputBlock[]]GetOutput([OutputBlock[]] $blocks, [int]$dspNum, [string]$tag ) {
        # return outputs matching the given dsp and tag (A or B)
        return $blocks | 
        Where-Object { $_.dspNum -eq $dspNum -and $_.whichOutput -eq $tag }
    }

    hidden [InputBlock]GetInput([InputBlock[]] $blocks, [int]$dspNum, [string]$tag ) {
        # return inputs matching the given dsp and tag (A or B)
        return $blocks | 
        Where-Object { $_.dspNum -eq $dspNum -and $_.whichInput -eq $tag }
    }
}
class Preset {
    [string[]] $topology = [string[]]::new(2)
    [System.Object] $hlx # dynamic object of the entire json preset 
    [Block[]] $allBlocks # array of all Blocks in all DSPs
    [InputBlock[]] $allInputBlocks # array of all input Blocks in all DSPs
    [OutputBlock[]] $allOutputBlocks # array of all output Blocks in all DSPs
    [string] $FQN
    [string] $Name
    [Dsp[]] $dsp = [Dsp[]]::new(2)

    <#

    Signatures of aggregate chains including those crossing from dsp0 to dsp1.
    Most of these will be $null for most presets, it depends on the input
    configuration. Each signature string represents the complete signal chain
    initiating from the given input. If the final output of any dsp0 chain is
    targeted to a dsp1 input, then the signature for that dsp0-initiated chain
    will include its continuation into dsp1. But that same dsp1 chain might ALSO
    initiate from an actual dsp1 external input in which case it's blocks will
    be represented on both the dsp0-initiated signature the dsp1-initiated
    signature. This matches how the Helix actually works.
    
    For example consider this preset 
    
        dsp0 has the SABJ-topology "A" with this signal chain
            dsp0.InputA.from.GuitarIn -> delay -> amp -> dsp0.OutputA.to.dsp1.InputA

        dsp1 has the SABJ-topology "A" with this signal chain
            dsp1.InputA.from.AuxIn -> reverb -> dsp1.OutputA.to.Main.Out

    the signatures in that preset will be

        $sig0A = input -> delay -> amp -> reverb -> output (this follows the signal chain into the reverb on dsp1)
        $sig0B: $null (because no input to dsp0.InputB)
        $sig1A = input -> reverb -> output (this is the chaing from dsp1 input only)
        $sig1B: $null (because no input to dsp1.InputB)
#>
  

    [string] $sig0A;
    [string] $sig0B;
    [string] $sig1A;
    [string] $sig1B;

    # aggregateChain has the same basic explanation as $sigNN, but holding the heads
    # of each signal chain. First index is DSP, second is Input.
    #
    #    $aggregateChain[0,0]  chain starting at dsp0 inputA, or $null
    #    $aggregateChain[0,1]  chain starting at dsp0 inputB, or $null
    #    $aggregateChain[1,0]  chain starting at dsp1 inputA, or $null
    #    $aggregateChain[1,1]  chain starting at dsp1 inputB, or $null

    [Block[, ]]$aggregateChain = [Block[, ]]::new(2, 2) 

    #region ------ CONSTRUCTORS ----------------------------------------------------------------------
    Preset([string] $fqn) {
        if ( $fqn -eq "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 16 with my presets\Setlist1-FACTORY 1\Preset042-BAS_Hire Me!.hlx") {
            $bkpt = 1
        }
        $this.hlx = (Get-Content $fqn | ConvertFrom-Json)
        $this.FQN = $fqn
        $this.Name = $this.hlx.data.meta.name
        $this.GetAllBlocks($this.hlx)
        $this.GetAllInputBlocks($this.hlx)
        $this.GetAllOutputBlocks($this.hlx)
        $this.topology[0] = $this.hlx.data.tone.global."@topology0";
        $this.topology[1] = $this.hlx.data.tone.global."@topology1";
        try {

            (0..1) | ForEach-Object {
                $dspNum = $_
                $this.dsp[$dspNum] = $this.GetDspStructure(
                    $this.hlx,
                    $this.topology[$dspNum], 
                    $this.allInputBlocks,
                    $this.allOutputBlocks,
                    $this.allBlocks,
                    $dspNum)
                $this.dsp[$dspNum].Dump($dspNum)
            }
        }
        catch {
            $bkpt = 1
        }
        $this.BuildAggregateChains()
    }
    #endregion---- CONSTRUCTORS ----------------------------------------------------------------------
    hidden [Dsp]GetDspStructure(
        [System.Object] $hlx,
        [string] $topology,
        [InputBlock[]] $allInputBlocks,
        [OutputBlock[]] $allOutputBlocks,
        [Block[]] $allBlocks,
        [int]$dspNum
    ) {
        return [Dsp]::new(
            $allInputBlocks,
            $allOutputBlocks,
            $allBlocks, 
            $topology,
            $dspnum,
            $hlx.data.tone."dsp$dspNum".split."@position",
            $hlx.data.tone."dsp$dspNum".join."@position")
    }
    hidden [void]GetAllBlocks([System.Object] $hlx) {
        $this.allBlocks = @()
        (0..1) |
        ForEach-Object {
            $dspNumber = $_
            $dspName = "dsp$_"
            (0..17) |
            ForEach-Object {
                $blockNumber = $_
                $blockName = "block$blockNumber"
                $structure = $hlx.data.tone."$dspName"."$blockName";
                if ( $structure) {
                    $this.allBlocks += [Block]::new(
                        $dspNumber,
                        $blockNumber,
                        $structure."@path",
                        $structure."@position",
                        $structure)
                }
            }
        }
    }
    hidden [void]GetAllInputBlocks([System.Object] $hlx) {
        $this.allInputBlocks = @()
        (0..1) |
        ForEach-Object {
            $dspNumber = $_
            $dspName = "dsp$_"
            ("A", "B") |
            ForEach-Object {
                $blockNumber = $_
                $blockName = "input$blockNumber"
                $structure = $hlx.data.tone."$dspName"."$blockName";
                if ( $structure) {
                    $this.allInputBlocks += [InputBlock]::new(
                        $dspNumber,
                        0,
                        $structure."@path",
                        $structure."@position",
                        $blockNumber,
                        $structure)
                }
            }
        }
    }
    hidden [void]GetAllOutputBlocks([System.Object] $hlx) {
        $this.allOutputBlocks = @()
        (0..1) |
        ForEach-Object {
            $dspNumber = $_
            $dspName = "dsp$_"
            ("A", "B") |
            ForEach-Object {
                $blockNumber = $_
                $blockName = "output$blockNumber"
                $structure = $hlx.data.tone."$dspName"."$blockName";
                if ( $structure) {
                    $this.allOutputBlocks += [OutputBlock]::new(
                        $dspNumber,
                        0,
                        $structure."@path",
                        $structure."@position",
                        $blockNumber,
                        $structure)
                }
            }
        }
    }
    hidden [int]GetLastBlockDspOutputTarget([Block[]]$blocks) {

        # if the last block of the given block list is an output targeted at
        # another DSP's input (as opposed to being targeted to an external
        # output), return the target number of that output. Otherwise return
        # $null. The target codes for a DSP's input are currently 2, 3, 4

        if ($blocks[-1] -is [OutputBlock] -and $blocks[-1].structure."@output" -in 2, 3, 4) {
            return $blocks[-1].structure."@output"
        }
        else {
            return $null
        }
    }
    hidden [Block]LastLPathBlock([Block] $start) {
        # follow $start's nextL links and return the last block on the chain
        [Block]$b = $start
        while ($b -and $b.nextL) {
            $b = $b.nextL
        }
        return $b
    }
    hidden [Block]LastRPathBlock([Block] $start) {
        # follow $start's nextL/nextR links and return the last block on the chain
        [Block]$b = $start
        while ( $b -and $b.nextL ) {
            if ( $b.nextR ) { $b = $b.nextR }
            else { $b = $b.nextL }
        }
        return $b
    }
    hidden [SplitBlock]FirstSplit([Block]$start) {
        # follow $start's nextL links and return the first SplitBlock encountered
        [Block]$b = $start
        while ( $b -and !($b -is [SplitBlock])) {
            $b = $b.nextL 
        }
        return $b
    }

    hidden [void] PropagateSplit([Block]$head, [Block]$split) {
        # [bool]$stop = $false
        [Block]$b = $head

        # while ($b -and !$stop) {
        while ($b) {
            $b.precedingSplit = $split
            if ( $b -is [SplitBlock]) {
                # $stop = $true 
                $split = $b
            }
            elseif ($b -is [JoinBlock]) {
                # closing current split, starting assigned
                # back up to this join's split's preceding split
                $split = $b.precedingSplit.precedingSplit 
            }
            # $stop = $b -is [JoinBlock] -or $b -is [SplitBlock]
            $b = $b.nextL
        }
    }

    # hidden [Block] DuplicateOf([Block]$b) { return $b.Clone($b) }
    hidden [List[Block]] GetLeafBlocks($head) {
        # return a list of terminal nodes given a start block
        
        # close enough...Probably a better way in C#, but this will let us id a
        # specific traversal without having to preset a bunc of "visited" flags
        # (for which I would need to do a traversal...oops)
        # $travId = (Get-Date).Ticks

        [List[Block]]$visited = [List[Block]]::new(100)
        [List[Block]]$leafBlocks = [List[Block]]::new()
        [Stack[SplitBlock]]$splits = [Stack[SplitBlock]]::new(5)
        [List[Block]]$resumes = [List[Block]]::new(5)
        $b = $head

        while ($b ) {
                
            # $b.travID = $travID
            
            [Block]$prev = $b

            # if this block was queued upt o resume processing we can remove it
            # from that list since we've now encountered it
            if ($resumes.Contains($b)) { 
                $resumes.Remove($b) 
            }

            if ($b -is [SplitBlock]) {
                $splits.Push($b) 
                $b = $b.nextL
            }
            elseif ($b -is [JoinBlock]) {
                # if this is the join of the last-pushed $split
                # pop the split and traverse its right path
                if ($splits.Count -gt 0 ) {
                    # we're going to go back and get the R path
                    # of the previous split but we still have
                    # to traverse from the join *forward*
                    # so queue that up
                    if ($b.nextL -and !$resumes.Contains($b.nextL)) { $resumes.Add($b.nextL) }
                    $b = $splits.Pop().nextR 
                }
                else {
                    # we've reached the join block for the second time, after
                    # traversing the R path of its matching split
                    $b = $b.nextL
                }
            }
            else {
                # regular block
                $b = $b.nextL
            }

            if (!$b) {
                # we've run past the end, $prev is the leaf
                if ( $leafBlocks -notcontains $prev) {
                    $leafBlocks.Add($prev) 
                }
                if ($splits.Count -gt 0 ) {
                    $b = $splits.Pop().nextR 
                }
                elseif ($resumes.Count -gt 0) {
                    $b = $resumes[-1];
                    $resumes.Remove($b);
                }
            }

        }
        return $leafBlocks
    }
    hidden [List[Block]] AllLeafBlocks() {

        # return a list of all terminal blocks reached by traversal from all
        # aggregate chain inputs. In other words start from each aggregate chain
        # input and collect every block that's the final block in any of those
        # traversals. We only process true external inputs here (i.e., we don't
        # process dsp1 inputs pactched from dsp0 outputs)

        [List[Block]]$leafBlocks = [List[Block]]::new()
        ( 0..1 ) | ForEach-Object {
            $dspNum = $_
            ( 0..1 ) | ForEach-Object {
                $inputNum = $_
                $inputHead = $this.aggregateChain[$dspNum, $inputNum];
                if ($inputHead -and 0 -ne $inputHead.structure."@input") {
                    $leafBlocks.AddRange($this.GetLeafBlocks($inputHead))
                }
            }
        }
        return $leafBlocks
    }
    hidden [void] CloseAnyOpenSplits() {
        
        # add a join at the end of any aggregate chains have open splits. An open
        # split is two path end blocks that originated from the same split. The
        # algorithm here is: ensure there is at least two open ends, because
        # otherwise there's definitely no open split, then get all leaf blocks
        # sorted by the id of their preceding split, then find and and join to
        # matching pairs

        [bool]$needToCheckAgain = $true

        while ( $needToCheckAgain ) {
            $needToCheckAgain = $false
            [List[Block]]$leafBlocks = $this.AllLeafBlocks()
            $leafBlocks = (
                $leafBlocks |
                Where-Object { $_.precedingSplit } |
                Sort-Object $_.precedingSplit.id
            )
            while ( $leafBlocks.Count -ge 2) {
                # if ($leafBlocks[0].precedingSplit.id -in $leafBlocks[1].upstreamSplitIds()) {
                if ($leafBlocks[0].precedingSplit.id -eq $leafBlocks[1].precedingSplit.id) {
                    $this.AddImpliedJoin($leafBlocks[0], $leafBlocks[1])
                    $leafBlocks.RemoveRange(0, 2)

                    # if we created any joins we'll need to do
                    # the traversal again to see if the new
                    # join is now part of open split
                    $needToCheckAgain = $true
                }
                else {
                    $leafBlocks.RemoveAt(0)
                }
            }
        }
    }
    hidden [void]AddImpliedJoin([Block]$b1, [Block]$b2) {

        [JoinBlock]$j = [JoinBlock]::new($b1.dspnum)
        $j.precedingSplit = $b1.precedingSplit
        $b1.NextL = $j
        $b2.NextL = $j
        
        # the new join will have its precedingSplit set to the precedingSplit of
        # b1 and b2, which is correct, but since we just closed an open split, we
        # really need there to be something *after* this implied split, to reflect
        # that the chain after the new join descended from the previous split
        # "back one level", rather than from the split we just closed. That way,
        # if there needs to yet another implied split after the one just added
        # (which would happen in the case of nested open splits) we have a block
        # reflecting that "back one level" split, so we know to join it with any
        # parallel path that might still be open. confusing...
        [ImpliedBlock]$dummy = [ImpliedBlock]::new()
        $dummy.precedingSplit = $j.precedingSplit.precedingSplit
        $j.nextL = $dummy
    }
    hidden [void] BuildAggregateChains() {

        ( 0..1 ) | ForEach-Object {
            $dspNum = $_
            ( 0..1 ) | ForEach-Object {
                $inputNum = $_
                $this.aggregateChain[$dspnum, $inputNum] = $null
            } 
        }

        ( 0..1 ) | ForEach-Object {
            $dspNum = $_
            ( 0..1 ) | ForEach-Object {

                $inputNum = $_
                $inputHead = $this.dsp[$dspNum].input[$inputNum];

                # find output block(s) at the end(s) of the chain. $endOutput will
                # be an array of up to two output blocks. $endOutput[0] will be
                # the output(s) arrived at when traversing the left path of any
                # split. $endOutput[1] will be from traversing the right path of
                # any split. If there is no split $endOutput will have only one
                # element
                if ($inputHead) {

                    [List[Block]]$endOutput = [List[Block]]::new(2)

                    $endOutput.Add($this.LastLPathBlock($inputHead)) # end of the Left traversal of the chain
                    [Block]$blk = $this.LastRPathBlock($inputHead) # start back at the beginning to do R traversal
                    
                    if ($blk -ne $endOutput[0] ) {
                        # if the end of the Left/Right traversal of the chain is
                        # not the same end as found by the Left traversal, add it
                        $endOutput.Add($blk)
                    }

                    # Attach any chains that end in Outputs targeting dsp1 inputs.
                    # Note that we can do this no matter which dsp's inputs we're
                    # starting with because when we are processing signal chains
                    # that INITIATE on dsp1, their outputs will always be terminal
                    # due to the architecture of the Helix. In other words, dsp1
                    # outputs never route to dsp0 or dsp1. Only dsp0 outputs can
                    # route to another dsp.

                    $endOutput |
                    ForEach-Object {
                        $thisOutput = $_
                        switch ($thisOutput.structure."@output") {
                            2 {
                                $thisOutput.nextL = $this.dsp[1].input[0] 
                                $this.PropagateSplit($thisOutput.nextL, $thisOutput.precedingSplit)
                            }
                            3 {
                                $thisOutput.nextL = $this.dsp[1].input[1] 
                                $this.PropagateSplit($thisOutput.nextL, $thisOutput.precedingSplit)
                            }
                            4 {
                                # The output routes to both dsp1 inputs. This is a
                                # "hidden" split in terms of aggregate signal
                                # path. It's not a split that shows up in an SABJ
                                # topology inside a single DSP but it does in fact
                                # introduce a parallel path in the aggregate
                                # signal chain. So we need to add a split here
                                # before connecting to the dsp1 inputs
                                [SplitBlock]$sBlock = [SplitBlock]::new($dspNum)
                                
                                $thisOutput.nextL = $sBlock
                                $sBlock.nextL = $this.dsp[1].input[0] 
                                $sBlock.nextR = $this.dsp[1].input[1] 

                                $sBlock.precedingSplit = $thisOutput.precedingSplit 
                                $this.PropagateSplit($sBlock.nextL, $sBlock)
                                $this.PropagateSplit($sBlock.nextR, $sBlock)

                            }
                        }
                    }
                    $this.aggregateChain[$dspnum, $inputNum] = $inputHead
                }
            }
        }
        $this.CloseAnyOpenSplits()
    }
}

$CategoryOf = @"
{
    "HD2_Looper": "Looper",
    "HD2_LooperOneSwitch": "Looper",
    "VIC_LooperShuffling": "Looper",
    "HD2_AppDSPFlowSplitY": "Split",
    "HD2_AppDSPFlowSplitAB": "Split",
    "HD2_AppDSPFlowSplitXOver": "Split",
    "HD2_AppDSPFlowSplitDyn": "Split",
    "HD2_AppDSPFlowJoin": "Merge"
    }
"@ | ConvertFrom-Json
    
$CategoryContents = @"
{
    "Looper": [
        "HD2_Looper",
        "HD2_LooperOneSwitch",
        "VIC_LooperShuffling"
    ],
    "Split": [
        "HD2_AppDSPFlowSplitY",
        "HD2_AppDSPFlowSplitAB",
        "HD2_AppDSPFlowSplitXOver",
        "HD2_AppDSPFlowSplitDyn"
    ],
    "Merge": [
        "HD2_AppDSPFlowJoin"
    ]
}
"@ | ConvertFrom-Json

# a non zero input (@input != 0) is an input mapped to a physical input jack. We
# don't really care which one - for the purpose of establishing the signal path,
# anything coming from a physical input is the start of a signal path

# a 0 input path may still be of interest, but only if it's referred to

# @output values. * represents the termination of a signal path
# 1 Multi output*
# 2 Path 2A
# 3 Path 2B
# 4 Path 2A+B
# 5 1/4" *
# 6 XLR *
# 7 Send 1/2 *
# 8 Send 3/4 *
# 9 Digital *
# 10 USB 1/2 *
# 11 USB 3/4 *
# 12 USB 5/6 *

$doSpecificFiles = $true
$showConnections = $true
if ( $doSpecificFiles ) {
    
    (
        # "C:\Users\PCAUDI~1\AppData\Local\Temp\Nvr Gng Bk Loopr.hlx",
        # "C:\Users\PCAUDI~1\AppData\Local\Temp\in outs.hlx",
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 16 with my presets\Setlist6-Sandbox\Preset021-US Double Nrm.hlx",
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 16 with my presets\Setlist8-TEMPLATES\Preset016-MIDI Bass Pedals.hlx",
    
        # # # regular split followed by interconnect split
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 07 Helix Floor Backup with 3.80\Setlist2-FACTORY 2\Preset047-Unicorns Forever.hlx",
    
        # # # two parallel sections, second one is an "interconnect split" (meaning, via output 1A to Input 2A+B)
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\2.92 2020 11 22 BEFORE UPGRADE 2.9 TO 3.0\Setlist1-FACTORY 1\Preset101-Sunbather.hlx",
    
        # # nested parallel sections
        "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\2.81 2019 08 12(2)\Setlist2-FACTORY 2\Preset083-Unicorn In A Box.hlx",
    
        # # interconnect split followed by join
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 07 Helix Floor Backup with 3.80\Setlist1-FACTORY 1\Preset120-You Shall Pass.hlx",
    
        # # SABJ with no A
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 16 with my presets\Setlist1-FACTORY 1\Preset050-BIG DUBB.hlx",
    
        # # SABJ - SABJ but dsp1 SABJ has no S or A modules and no external input. So it will have null S or A on the intermediate lists
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 16 with my presets\Setlist1-FACTORY 1\Preset056-WATERS IN HELL.hlx",

        # # SAB with no A, feeding to ABJ
        # "C:\Users\PCAUDI~1\AppData\Local\Temp\sab no a - abj.hlx",
    
        # "C:\Users\PCAUDI~1\AppData\Local\Temp\sab - abj.hlx",
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 16 with my presets\Setlist1-FACTORY 1\Preset042-BAS_Hire Me!.hlx",
    
        # # "working" but not sure if I should do as described in the comment below
        # # doesn't crash but illustrates that i'm not treating an input as a split when multiple paths start from teh same input. Not sure i've thought this through but probably I should be doing that
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 07 Helix Floor Backup with 3.80\Setlist2-FACTORY 2\Preset051-Knife Fight.hlx",
    
        # # currently not working
    
        # # SAB with no A -- doesn't crash but doesn't handle parallel section right.    
        # # I think I need to generalize the solution to the 2A+B outputs where I insert
        # # an implied join. But instead of doing it in that one case, I need to do it
        # # whenever I have an open Split and I hit a terminal output on both paths.
        # "C:\Users\PCAUDI~1\AppData\Local\Temp\sab no a.hlx",

        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 16 with my presets\Setlist1-FACTORY 1\Preset043-Justice Fo Y'all.hlx",
        # "C:\Users\PCAUDI~1\AppData\Local\Temp\New Preset.hlx",
        # "C:\Users\PCAUDI~1\AppData\Local\Temp\x.hlx",
        # "C:\Users\PCAUDI~1\AppData\Local\Temp\y.hlx",
        # "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\2.92 2020 11 22 BEFORE UPGRADE 2.9 TO 3.0\Setlist1-FACTORY 1\Preset101-Sunbather.hlx",
        $null
    )  |
    ForEach-Object {
        if (! $_) { return }
        $global:blockInstanceID = 1
        $presetFQN = $_
        $fiPreset = Get-Item $presetFQN
        $pre = [Preset]::new($presetFQN)
        DumpAggregateChains $pre -ShowConnections:$showConnections
    }
}
else {
    # do a whole directory tree
    Get-ChildItem "E:\All\Documents\Line 6\Tones\Helix\Backup - Whole System\3.80 2024 12 16 with my presets" -Recurse -Include *.hlx |
    ForEach-Object {
        if ($_.BaseName -notmatch "New Preset") {
            $global:blockInstanceID = 1
            $pre = [Preset]::new($_.FullName)
            DumpAggregateChains $pre -ShowConnections:$showConnections
        }
    }
            
}