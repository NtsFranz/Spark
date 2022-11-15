<script>
    import Minimap from "$lib/components/Minimap.svelte";
    import CompactMainBanner from "$lib/components/CompactMainBanner.svelte";
    import PlayerLists from "$lib/components/PlayerLists.svelte";
    import EventLog from "$lib/components/EventLog.svelte";
    import CompactMinimap from "$lib/components/CompactMinimap.svelte";
    import PlayerListOrange from "$lib/components/PlayerListOrange.svelte";
    import PlayerListBlue from "$lib/components/PlayerListBlue.svelte";
    import {onDestroy} from "svelte";
    import PositionableComponent from "$lib/components/PositionableComponent.svelte";
    import {SparkWebsocket} from '$lib/js/spark_websocket.js';


    let sw = new SparkWebsocket();
    let config = {};
    sw.subscribe("overlay_config", data => {
        config = data;
    });

    onDestroy(() => sw.close());
</script>

<main>
    <div class="no-overflow">

        {#if config && config['visibility']}
            {#if config['visibility']['main_banner']}
                <PositionableComponent config_key="compact_main_banner" position_data={{
					"top": 0.04,
					"bottom": 0.5,
					"left": 0.5,
					"right": 0.5,
					"scale": 1,
					"center_horizontally": true
				  }}>
                    <CompactMainBanner/>
                </PositionableComponent>
            {/if}

            {#if config['visibility']['player_rosters']}
                <PositionableComponent config_key="player_list_orange" position_data={{
					"top": 0.03,
					"bottom": 0.5,
					"left": 0.03,
					"right": 0.5,
					"scale": 1,
					"center_horizontally": false
				  }}>
                    <PlayerListOrange/>
                </PositionableComponent>
            {/if}

            {#if config['visibility']['player_rosters']}
                <PositionableComponent config_key="player_list_blue" position_data={{
					"top": 0.03,
					"bottom": 0.5,
					"left": 0.5,
					"right": 0.03,
					"scale": 1,
					"center_horizontally": false
				  }}>
                    <PlayerListBlue/>
                </PositionableComponent>
            {/if}


            {#if config['visibility']['minimap']}
                <PositionableComponent config_key="minimap" position_data={{
					"top": 0.5,
					"bottom": 0.03,
					"left": 0.5,
					"right": 0.03,
					"scale": 1,
					"center_horizontally": false
				  }}>
                    <Minimap/>
                </PositionableComponent>
            {/if}


            {#if config['visibility']['compact_minimap']}
                <PositionableComponent config_key="compact_minimap" position_data={{
					"top": 0.5,
					"bottom": 0.03,
					"left": 0.5,
					"right": 0.03,
					"scale": 1,
					"center_horizontally": false
				  }}>
                    <CompactMinimap/>
                </PositionableComponent>
            {/if}

            {#if config['visibility']['event_log']}
                <PositionableComponent config_key="event_log" position_data={{
					"top": 0.5,
					"bottom": 0.03,
					"left": 0.03,
					"right": 0.5,
					"scale": 1,
					"center_horizontally": false
				  }}>
                    <EventLog/>
                </PositionableComponent>
            {/if}
        {/if}
    </div>

</main>

<style>
    .no-overflow {
        overflow: hidden;
        position: absolute;
        height: 100%;
        width: 100%;
        top: 0;
        left: 0;
    }
</style>
