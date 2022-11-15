<svelte:head>
    <title>Minimap Overlay</title>
</svelte:head>

<script>
    import Minimap from "$lib/components/Minimap.svelte";
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
    <Minimap frame={frame}/>
</div>