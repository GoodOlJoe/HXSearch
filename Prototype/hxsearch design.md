# Build a Preset's Chains 

~~~
# build list of all blocks dsp*.block* so we can use Linq 

dsp0 split position = dsp0.split.@position
dsp0 join position = dsp0.join.@position

chain 1A (and same for 2A, but with dsp1)
    head
        grab all blocks where @path=0 && @position < dsp0.split.@position
    tail
        grab all blocks where @path=0 && @position >= dsp0.join.@position
    a
        grab all blocks where @path=0 && @position >= dsp0.split.@position && @position < dsp0.join.@position
    b
        grab all blocks where @path=1

chain 1B  (and same for 2B, but with dsp1)
    # split never applies to 1B
    head
        grab all blocks where @path=1
    tail
        grab all blocks where @path=0 && @position >= dsp0.join.@position
    a # 1B cannot have parallel paths so: no a, no b
    b # 1B cannot have parallel paths so: no a, no b
~~~