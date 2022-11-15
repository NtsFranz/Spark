<script lang="ts">
    export let frame;

    function x_pos(z) {
        return (-z / 80 + .5) * 427 + 68;
    }

    function y_pos(x) {
        return (x / 32 + .5) * 165 + 5;
    }
</script>

<style>
    .minimap_container {
        position: relative;
        width: 35.3125em;
        height: 10.875em;
        overflow: hidden;
    }

    .minimap_image {
        height: 100%;
        display: block;
        margin: auto;
    }

    .minimap_disc {
        width: 25px;
        position: absolute;
        top: -12.5px;
        left: -12.5px;
    }

    .player {
        position: absolute;
    }

    .player > div {
        border-radius: 100px;
        width: 30px;
        height: 30px;
        position: absolute;
        top: -15px;
        left: -15px;
    }

    .player.possession > div:before {
        content: '';
        position: absolute;
        top: -5px;
        left: -5px;
        height: 36px;
        width: 36px;
        background: #0000;
        border-radius: 100px;
        z-index: 1;
        border: 2px solid white;
    }

    .player > p {
        font-family: Arial, sans-serif;
        color: white;
        font-weight: bold;
        text-align: center;
        position: absolute;
        top: -11px;
        left: -12px;
        margin: 0;
        font-size: 20px;
    }

    .blue > div {
        background-color: #0199ff;
    }

    .orange > div {
        background-color: #ff8d04;
    }

    .positionable_element {
        position: absolute;
        top: 86.5px;
        left: 281.5px;
        transition: all .3s;
    }

    #timeout_banner {
        z-index: 1000;
        position: absolute;
        width: 100%;
        margin: 0;
        top: 0;
        visibility: hidden;
    }

    #timeout_banner > img {
        width: 100%;
    }
</style>

<div class="minimap_container">

    <img class="minimap_image" src="/img/minimap_raw.png" draggable="false" alt="">

    {#if frame}

        {#if frame['teams'][0]['players']}
            {#each frame['teams'][0]['players'] as p}
                <div class="player blue positionable_element" class:possession={p['possession']}
                     style="left:{x_pos(p['head']['position'][2])}px; top: {y_pos(p['head']['position'][0])}px;">
                    <div></div>
                    <p class="player_number">{p['number'].toString().padStart(2, '0')}</p>
                </div>
            {/each}
        {/if}


        {#if frame['teams'][1]['players']}
            {#each frame['teams'][1]['players'] as p}
                <div class="player orange positionable_element" class:possession={p['possession']}
                     style="left:{x_pos(p['head']['position'][2])}px; top: {y_pos(p['head']['position'][0])}px;">
                    <div></div>
                    <p class="player_number">{p['number'].toString().padStart(2, '0')}</p>
                </div>
            {/each}
        {/if}


        {#if frame}
            <div id="disc" class="positionable_element"
                 style="left:{x_pos(frame['disc']['position'][2])}px; top: {y_pos(frame['disc']['position'][0])}px;">
                <img class="minimap_disc" src="/img/minimap_disc.png" draggable="false">
            </div>
        {/if}
    {/if}
</div>