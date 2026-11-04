<script lang="ts">
    export let frame;

    // Arena is 80m long (z) and 32m wide (x); map those onto the minimap image.
    function x_pos(z) {
        return (-(Number(z) || 0) / 80 + .5) * 427 + 68;
    }

    function y_pos(x) {
        return ((Number(x) || 0) / 32 + .5) * 165 + 5;
    }

    // The game API reports player positions under head.position (newer builds)
    // or position (older builds). Missing data must never break the render.
    function pos(obj) {
        const p = obj?.head?.position ?? obj?.position;
        return Array.isArray(p) && p.length >= 3 ? p : [0, 0, 0];
    }

    function num(p) {
        return String(p?.number ?? 0).padStart(2, '0');
    }

    $: blue = Array.isArray(frame?.teams?.[0]?.players) ? frame.teams[0].players : [];
    $: orange = Array.isArray(frame?.teams?.[1]?.players) ? frame.teams[1].players : [];
    $: disc = frame?.disc ? pos(frame.disc) : null;
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

    {#each blue as p}
        <div class="player blue positionable_element" class:possession={p?.possession}
             style="left:{x_pos(pos(p)[2])}px; top: {y_pos(pos(p)[0])}px;">
            <div></div>
            <p class="player_number">{num(p)}</p>
        </div>
    {/each}

    {#each orange as p}
        <div class="player orange positionable_element" class:possession={p?.possession}
             style="left:{x_pos(pos(p)[2])}px; top: {y_pos(pos(p)[0])}px;">
            <div></div>
            <p class="player_number">{num(p)}</p>
        </div>
    {/each}

    {#if disc}
        <div id="disc" class="positionable_element"
             style="left:{x_pos(disc[2])}px; top: {y_pos(disc[0])}px;">
            <img class="minimap_disc" src="/img/minimap_disc.png" draggable="false" alt="">
        </div>
    {/if}
</div>
