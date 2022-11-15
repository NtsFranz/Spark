<svelte:head>
    <title>IgniteVR Metrics Branding</title>
</svelte:head>

<style>
    @font-face {
        font-family: 'ubuntu';
        font-weight: 700;
        src: url('/fonts/ubuntu-700.woff2') format('woff2'), url('/fonts/ubuntu-700.ttf') format('truetype');
    }
    
    .grid_container {
        display: flex;
        flex-direction: row;
        flex-wrap: wrap;
        position: absolute;
        top: 200px;
        left: 0;
        margin: 0;
        padding: 0;
    }

    .grid_container > div {
        width: 800px;
        height: 200px;
        border: 1px solid black;
        position: relative;
    }

    .grid_container > div div {
        position: absolute;
        width: 340px;
        line-height: 0;
        top: 100px;
        font-family: 'ubuntu', Inconsolata, sans-serif;
        font-size: 32px;
        font-weight: 900;
    }

    .grid_container > div div:first-child {
        text-align: right;
        left: 0;
        animation: animated_branding_left 1s;
    }

    .grid_container > div div:last-child {
        right: 0;
        animation: animated_branding_right 1s;
    }

    .grid_container > div img {
        width: 128px;
        height: 128px;
        margin: auto;
        z-index: 2;
        animation: animated_branding_img 1s;
    }

    .grid_container .choice2 {
        color: white;
        text-shadow: 0 0 10px #0008;
    }

    .grid_container .choice3 {
        color: black;
    }


    .grid_container .choice4 {
        color: white;
        text-shadow: 0 0 10px #0008;
        -webkit-text-stroke: 1px black;
    }


    .grid_container .choice5 {
        color: white;
        text-shadow: 0 0 10px #0008;
        -webkit-text-stroke: 1px black;
    }

    @keyframes animated_branding_img {
        0% {
            transform: scale(.5);
            opacity: 0;
        }

        100% {
            transform: scale(1);
            opacity: 1;
        }
    }

    @keyframes animated_branding_left {
        0%, 10% {
            opacity: 0;
            transform: translateX(50px);
        }
        20% {
            opacity: 0;
        }

        100% {
            transform: translateX(0);
            opacity: 1;
        }
    }

    @keyframes animated_branding_right {
        0%, 10% {
            opacity: 0;
            transform: translateX(-50px);
        }
        20% {
            opacity: 0;
        }

        100% {
            transform: translateX(0);
            opacity: 1;
        }
    }
</style>


<div id="background_container" class="branding">
    <h3 style="margin-left: 1em;">IgniteVR Metrics Branding. Use these (or your own) before, during, or after the
        scoreboard overlays provided
        by Ignite.
        <br>
        <br>
        Import this page as a browser source in OBS, then crop to choose the one you want.
    </h3>

    <div class="grid_container">

        <div class="choice1" style="display: flex;">
            <div>Powered By</div>
            <img src="/img/ignite_logo_primary_256.png" alt=""/>
            <div>IgniteVR Metrics</div>
        </div>

        <div class="choice2" style="display: flex;">
            <div>Powered By</div>
            <img src="/img/ignite_logo_primary_white_256.png" alt=""/>
            <div>IgniteVR Metrics</div>
        </div>


        <div class="choice3" style="display: flex;">
            <div>Powered By</div>
            <img src="/img/ignite_logo_primary_white_256.png" style="filter: invert(1);" alt=""/>
            <div>IgniteVR Metrics</div>
        </div>


        <div class="choice4" style="display: flex;">
            <div>Powered By</div>
            <img src="/img/ignite_logo_primary_white_256.png" alt=""/>
            <div>IgniteVR Metrics</div>
        </div>

        <div class="choice5" style="display: flex;">
            <div>Powered By</div>
            <img src="/img/ignite_logo_primary_256.png" alt=""/>
            <div>IgniteVR Metrics</div>
        </div>

    </div>
</div>