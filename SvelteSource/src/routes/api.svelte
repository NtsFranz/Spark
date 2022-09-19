<svelte:head>
    <title>Match Setup</title>
    <link rel="stylesheet" href="/css/lib/bulma.min.css">
    <link rel="stylesheet" href="/css/styles.css">
    <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@4.5.0/swagger-ui.css"/>
    <script defer src="https://unpkg.com/swagger-ui-dist@4.5.0/swagger-ui-bundle.js" crossorigin></script>
</svelte:head>

<style>
    h1 {
        font-size: 2em;
    }

    @font-face {
        font-family: 'Inconsolata';
        src: url('$lib/fonts/Inconsolata.woff2') format('woff2'), url('$lib/fonts/Inconsolata.ttf') format('truetype');
    }

    body {
        font-family: 'Inconsolata', monospace;
    }

    .content ul li {
        list-style: none;
        margin-top: 2em;
    }

    :global(.scheme-container) {
        display: none;
    }

    :global(.information-container.wrapper) {
        display: none;
    }
</style>

<script>
    import {onMount} from "svelte";

    onMount(()=>{
        window.ui = SwaggerUIBundle({
            url: '/openapi.yaml',
            dom_id: '#swagger-ui',
        });
    });
</script>

<body>
<!-- <div class="watermark">
	<img src="img/secondary_white.png">
</div> -->
<section class="hero is-medium">
    <div class="hero-body" style="background-color: #0003;padding: 4rem 1.5rem; overflow: hidden">
        <div class="container has-text-centered">
            <h1>Spark API</h1>
            <img style="float:left;width: 10em;position: absolute;left: 10em;bottom: 0;opacity: .1;transform: scale(6); z-index: -1"
                 src="/img/ignite_logo.png" alt="ignite logo">
            <p class="subtitle" style="font-size: 1.2em;">
                The Spark API can be used to control various aspects of Spark using 3rd party tools such as AutoHotKey
                or get data from the game for your own custom overlays or tools.
            </p>

        </div>
    </div>
</section>
<div class="content" style="max-width: 60em; margin: 0 auto;">

    <nav class="breadcrumb" aria-label="breadcrumbs">
        <ul>
            <li><a href="/">Home</a></li>
            <li class="is-active"><a href="#" aria-current="page">Spark API</a></li>
        </ul>
    </nav>

    <div class="box">
        <div id="swagger-ui"></div>
    </div>
</div>
</body>
