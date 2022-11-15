<svelte:head>
    <link rel="stylesheet" type="text/css" href="http://localhost:6724/css/autocomplete_styles.css">
    <script type="text/javascript" src="http://localhost:6724/js/autocomplete.js"></script>

    <script src="http://localhost:6724/js/util.js"></script>
    <script src="http://localhost:6724/js/fetch_utils.js"></script>
</svelte:head>

<script>
    import 'bulma';
</script>

<style global>

    :root {
        --ignite-orange: #f27d18;
        --echo-stats-header-orange: #240407;
        --echo-stats-header-blue: #0b0b13;
        --echo-stats-row-dark: rgba(0, 0, 0, 0.4);
        --echo-stats-row-dark-hover: rgba(0, 0, 0, 0.5);
        --echo-stats-table-orange: rgba(255, 150, 50, 0.7);
        --echo-stats-table-blue: rgba(50, 150, 255, 0.7)
    }
    
    @font-face {
        font-family: 'Inconsolata';
        src: url('$lib/fonts/Inconsolata.woff2') format('woff2'), url('$lib/fonts/Inconsolata.ttf') format('truetype');
    }
    
    body {
        font-family: 'Inconsolata', monospace;
    }

    h1 {
        font-size: 2em;
    }

    .content ul li {
        list-style: none;
        margin-top: 2em;
    }

</style>

<slot/>