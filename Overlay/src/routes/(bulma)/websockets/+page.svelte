<svelte:head>
    <title>Spark Websocket API</title>
    
    <!-- Syntax Highlighting -->
<!--    <link rel="stylesheet" href="/css/lib/prism.css">-->
<!--    <script defer src="/js/lib/prism.js"></script>-->
</svelte:head>

<script>
    // import Prism from 'prismjs';
    import Header from "$lib/components/Header.svelte";
    import "$lib/css/prism.css";
    import '$lib/js/prism.js';
</script>

<style>
    .content ul li {
        list-style: initial;
        margin-top: initial;
    }
</style>

<Header title="Spark Websocket API" subtitle="Subscribe to events in the browser. This makes event-driven overlays very simple."/>


<div class="content" style="max-width: 60em; margin: auto;">

    <nav class="breadcrumb" aria-label="breadcrumbs">
        <ul>
            <li><a href="/">Home</a></li>
            <li class="is-active"><a href="#" aria-current="page">Websockets</a></li>
        </ul>
    </nav>

    <div class="box">
        This API allows you to subscribe to any number of events and immediately get a custom json response back with
        information about that event. This is more efficient and more responsive than a polling-based approach, and it
        makes organization much simpler. Without this feature, event detection and parsing would have to happen in the
        browser, but Spark already does this, so why not share the data.

        <br>
        <h3>List of available events:</h3>
        <ul>
            <li>goal</li>
            <li>save</li>
            <li>steal</li>
            <li>stun</li>
            <li>joust</li>
            <li>pause</li>
            <li>joined_game</li>
            <li>left_game</li>
            <li>frame_1hz</li>
            <li>frame_10hz</li>
            <li>frame_30hz</li>
        </ul>
        <h3>List of events that immediately respond with the current state, then send updates on change:</h3>
        <ul>
            <li>overlay_config</li>
        </ul>

        <h3>Usage:</h3>
        <p>Add this code to the bottom of your &lt;body&gt; tag in your .html file. Instead of console.log(), modify the
            visibility or content of various elements on your page.</p>
        
        <pre><code class="language-js">{`<script src="http://localhost:6724/js/spark_websocket.js"></script>
			
<script>
	// initialize the websocket connection
	let sw = new SparkWebsocket();
	
	// subscribe to any number of events and supply a callback
	sw.subscribe("goal", data => {
            // process the event here
            console.log(data);
        });
			
	sw.subscribe("joust", data => {
            console.log(data);
        });
</script>`}</code></pre>

    </div>
</div>
