<script lang="ts">
    import {onDestroy, onMount} from "svelte";
    import {SparkWebsocket} from "$lib/js/spark_websocket.js";

    let container;

    let sw = new SparkWebsocket();
    onMount(()=>{
        sw.subscribe("event_log", data => {
            if (data == null) return;
            console.log(data);

            let elem = document.createElement("div");
            elem.innerText = data["message"];
            elem.classList.add('event');
            container.prepend(elem);

            setTimeout(() => {
                elem.classList.add("invisible");
                setTimeout(() => {
                    elem.remove();
                }, 500);
            }, 5000);
        });
    });

    onDestroy(() => sw.close());

</script>

<style>
  #event_log_container {
    display: flex;
    flex-direction: column-reverse;
    height: 40em;
	min-width: 40em;
  }

  :global(.event) {
    display: inline-block;
    margin: 0.4em;
    padding: 0.6em;
    background-color: black;
    border-radius: 0.5em;
    color: white;
    font-weight: 900;
    font-family: Inconsolata, monospace;
    width: fit-content;
    animation: movein .5s;
    transition: opacity .5s;
  }

  @keyframes movein {
    0% {
      opacity: 0;
      height: 0;
      margin: 0;
      padding: 0;
    }

    50% {
      opacity: 0;
      height: auto;
      margin: 0.4em;
      padding: 0.6em;
    }


    100% {
      opacity: 1;
    }
  }

  :global(.invisible) {
    opacity: 0;
  }
</style>

<div bind:this={container} id="event_log_container"></div>