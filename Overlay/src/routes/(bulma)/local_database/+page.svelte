<svelte:head>
    <title>Local Database</title>
</svelte:head>

<style>

    input {
        font-size: .8em;
    }

    #swap_sides_button.isLoading img {
        opacity: 0;
    }


    .checkbox_grid > label {
        flex-grow: 1;

    }

    .orange {
        color: #d18100;
    }

    .blue {
        color: #0073d1;
    }
</style>

<Header title="Local Database" subtitle="Explore data from all of your locally recorded matches."/>

<div class="content" style="max-width: 80em; margin: auto;">

    <nav class="breadcrumb" aria-label="breadcrumbs" style="position: relative; top: -2em;">
        <ul>
            <li><a href="/">Home</a></li>
            <li class="is-active"><a href="#" aria-current="page">Local Database</a></li>
        </ul>
    </nav>
    <div class="box" style="position: relative; top: -2em;">
        <button class="button" on:click={refresh} style="position: absolute; top:-3em; right: 0;">Refresh</button>

        <div class="match_setup_flex" style="position: relative; max-height: 50em; overflow-y: scroll;">
            <div>
                <h2>Jousts</h2>
                <p>Limited to the last 1,000 jousts for performance.</p>
                <table class="table is-striped is-narrow is-hoverable">
                    <thead>
                    <tr>
                        <th><a on:click={joustsByMatchTime}>Match Time</a></th>
                        <th>Session ID</th>
                        <th>Game Clock</th>
                        <th>Player Name</th>
                        <th>Type</th>
                        <th><a on:click={joustsByTime}>Time</a></th>
                        <th>Max Speed</th>
                        <th>Tube Exit Speed</th>
                    </tr>
                    </thead>
                    <tbody>
                    {#each jousts as joust}
                        <tr>
                            <td>{joust['match_time']}</td>
                            <td>{joust['session_id'].substring(0, 3)}
                                ...{joust['session_id'].substring(joust['session_id'].length - 3)}</td>
                            <td>{joust['game_clock'].toFixed(2)} s</td>
                            <td>{joust['player_name']}</td>
                            {#if joust['event_type'] === 'defensive_joust'}
                                {#if joust['other_player_name'] === 'orange'}
                                    <td class="orange">Defensive</td>
                                {:else if joust['other_player_name'] === 'blue'}
                                    <td class="blue">Defensive</td>
                                {:else}
                                    <td>Defensive</td>
                                {/if}
                            {:else if joust['event_type'] === 'joust_speed'}
                                <td>Neutral</td>
                            {:else}
                                <td>{joust['event_type']}</td>
                            {/if}
                            <td>{joust['z2'].toFixed(2)} s</td>
                            <td>{joust['x2'].toFixed(1)} m/s</td>
                            <td>{joust['y2'].toFixed(1)} m/s</td>
                        </tr>
                    {/each}

                    </tbody>
                </table>


            </div>


        </div>
    </div>


    <div class="box">
        <div class="match_setup_flex" style="position: relative; max-height: 50em; overflow-y: scroll;">
            <div>
                <h2>All Events</h2>
                <p>Limited to the last 1,000 events for performance.</p>
                <table class="table is-striped is-narrow is-hoverable">
                    <thead>
                    <tr>
                        <th>Match Time</th>
                        <th>Session ID</th>
                        <th>Game Clock</th>
                        <th>Type</th>
                        <th>Player Name</th>
                        <th>Other Player Name</th>
                    </tr>
                    </thead>
                    <tbody>
                    {#each events as e}
                        <tr>
                            <td>{e['match_time']}</td>
                            <td>{e['session_id'].substring(0, 3)}
                                ...{e['session_id'].substring(e['session_id'].length - 3)}</td>
                            <td>{e['game_clock'].toFixed(2)} s</td>
                            <td>{e['event_type']}</td>
                            <td>{e['player_name']}</td>
                            <td>{e['other_player_name']}</td>
                        </tr>
                    {/each}

                    </tbody>
                </table>


            </div>


        </div>
    </div>
</div>


<script>
    import {onMount} from "svelte";
    import Header from "$lib/components/Header.svelte";

    let jousts = [];
    let events = [];
    let joustTimeDesc = false;
    let joustMatchTimeDesc = false;

    function refresh() {
        joustTimeDesc = false;
        joustMatchTimeDesc = false;
        fetch('http://localhost:6724/api/db/jousts').then(r => r.json()).then(r => jousts = r).then(r => joustsByMatchTime());
        fetch('http://localhost:6724/api/db/events').then(r => r.json()).then(r => events = r);
    }

    function joustsByTime() {
        console.log("Sorting by Joust Time");
        if (joustTimeDesc) {
            jousts = jousts.sort((a, b) => b['z2'] - a['z2']);
        } else {
            jousts = jousts.sort((a, b) => a['z2'] - b['z2']);
        }
        joustTimeDesc = !joustTimeDesc;
    }

    function joustsByMatchTime() {
        console.log("Sorting by Match Time");
        if (joustMatchTimeDesc) {
            jousts = jousts.sort((a, b) => b['game_clock'] - a['game_clock']);
            jousts = jousts.sort((a, b) => a['match_time'].localeCompare(b['match_time']));
        } else {
            jousts = jousts.sort((a, b) => a['game_clock'] - b['game_clock']);
            jousts = jousts.sort((a, b) => b['match_time'].localeCompare(a['match_time']));
        }
        joustMatchTimeDesc = !joustMatchTimeDesc;
    }

    onMount(() => {
        refresh();
    });
</script>