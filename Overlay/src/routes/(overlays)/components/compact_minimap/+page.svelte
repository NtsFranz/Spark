<svelte:head>
    <title>Compact Minimap Overlay</title>
</svelte:head>

<script>
    import CompactMinimap from "$lib/components/CompactMinimap.svelte";
    import {SparkWebsocket} from "$lib/js/spark_websocket.js";
    import {onDestroy} from "svelte";

    let frame;

    let sw = new SparkWebsocket();
    sw.subscribe("frame_30hz", data => {
        frame = data;
    });

    onDestroy(() => sw.close());
</script>


<div>
    <CompactMinimap frame={frame}/>
</div>