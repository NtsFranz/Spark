<svelte:head>
    <title>Match Setup</title>

    <link rel="stylesheet" type="text/css" href="/css/autocomplete_styles.css">
    <script type="text/javascript" src="/js/autocomplete.js"></script>

    <script src="/js/util.js"></script>
    <script src="/js/fetch_utils.js"></script>
</svelte:head>


<style>

    .team_logo_img {
        width: 6em;
    }

    input {
        font-size: .8em;
    }

    #swap_sides_button.isLoading img {
        opacity: 0;
    }


    .orange.input,
    .orange.textarea {
        border-color: #d18100
    }

    .orange.input:active,
    .orange.input:focus,
    .orange.is-active.input,
    .orange.is-active.textarea,
    .orange.is-focused.input,
    .orange.is-focused.textarea,
    .orange.textarea:active,
    .orange.textarea:focus {
        box-shadow: 0 0 0 .125em rgba(209, 84, 0, 0.25)
    }

    .blue.input,
    .blue.textarea {
        border-color: #0073d1
    }

    .blue.input:active,
    .blue.input:focus,
    .blue.is-active.input,
    .blue.is-active.textarea,
    .blue.is-focused.input,
    .blue.is-focused.textarea,
    .blue.textarea:active,
    .blue.textarea:focus {
        box-shadow: 0 0 0 .125em rgba(0, 63, 209, 0.25)
    }

    .hide {
        display: none !important;
    }

    .checkbox_grid {
        display: flex;
        flex-direction: column;
        flex-wrap: wrap;
    }

    .checkbox_grid > label {
        flex-grow: 1;

    }
</style>

<Header title="Match Setup" subtitle="Enter custom team names and logos here."/>

<div class="content" style="max-width: 60em; margin: auto;">

    <div class="box" style="position: relative; top: -2em;font-size: 1.5em;">

        <div class="match_setup_flex">
            <div>
                <table class="match_selection_table manual_input">
                    <thead>
                    <tr class="match_group_item">
                        <th class="home_team_name_head" colspan="2">Orange Team</th>
                        <th></th>
                        <th class="away_team_name_head" colspan="2">Blue Team</th>
                    </tr>
                    </thead>
                    <tbody>
                    <tr>
                        <td>
                            <img class="team_logo_img team_logo_orange" src="{config['teams'][1]['team_logo']}" alt=""/>
                        </td>

                        <td>
                            Team Name:
                            <form onsubmit="return false" class="custom_team_input_form">
                                <input autocomplete="off" type="text"
                                       class="input orange team_name_input_orange team_input force_visible"
                                       bind:value="{config['teams'][1]['team_name']}" on:change={manualValueChanged}>
                            </form>
                            <br>
                            Logo URL:<br>
                            <input class="input orange logo_url_input team_logo_input_orange"
                                   bind:value={config['teams'][1]['team_logo']} on:change={manualValueChanged}>
                        </td>

                        <td style="padding: 3em 0;">
                            <div class="button is-dark" id="swap_sides_button" style="margin: auto;"
                                 class:is-loading={isLoading}
                                 on:click={swapSides}>
                                <img src="/img/swap-horizontal-bold.png"
                                     style="height: 1.5em;margin-top: -0.2em;" alt="swap sides button">
                            </div>
                        </td>

                        <td>
                            Team Name:
                            <form onsubmit="return false" class="custom_team_input_form">
                                <input autocomplete="off" type="text"
                                       class="input blue team_name_input_blue team_input force_visible"
                                       style="text-align: right;" bind:value="{config['teams'][0]['team_name']}"
                                       on:change={manualValueChanged}>
                            </form>
                            <br>
                            Logo URL:<br>
                            <input class="input blue logo_url_input team_logo_input_blue"
                                   bind:value="{config['teams'][0]['team_logo']}" on:change={manualValueChanged}>
                        </td>

                        <td>
                            <img class="team_logo_img team_logo_blue" src="{config['teams'][0]['team_logo']}"/>
                        </td>
                    </tr>


                    <tr>
                        <td colspan="5" style="font-size: .6em;">
                            Automatic team names/logos are detected based on rosters on the VRML website.
                        </td>
                    </tr>
                    <tr>
                        <td colspan="5"><label class="checkbox" style="margin-left: 3em;">
                            <input checked={config['team_names_source']=== 1}
                                   type="checkbox" on:click={(e)=>{setTeamNamesSource(e.target.checked);}}/>
                            Use automatic team names/logos
                        </label></td>
                    </tr>

                    </tbody>
                </table>

                <h4>Overlay Configuration</h4>
                <div class="checkbox_grid">

                    <VisibilityConfigCheckbox key="minimap" text="Show default minimap"
                                              checked="{config['visibility']['minimap']}"/>
                    <VisibilityConfigCheckbox key="compact_minimap" text="Show compact minimap"
                                              checked="{config['visibility']['compact_minimap']}"/>
                    <VisibilityConfigCheckbox key="player_rosters" text="Show player rosters"
                                              checked="{config['visibility']['player_rosters']}"/>
                    <VisibilityConfigCheckbox key="main_banner" text="Show main banner"
                                              checked="{config['visibility']['main_banner']}"/>
                    <VisibilityConfigCheckbox key="show_team_logos" text="Show team logos"
                                              checked="{config['visibility']['show_team_logos']}"/>
                    <VisibilityConfigCheckbox key="show_team_names" text="Show team names"
                                              checked="{config['visibility']['show_team_names']}"/>
                    <VisibilityConfigCheckbox key="event_log" text="Show event log"
                                              checked="{config['visibility']['event_log']}"/>
                    <!--					<label class="checkbox" style="margin-left: 4em;">-->
                    <!--						<input type="checkbox" id="show_team_names_checkbox"/>-->
                    <!--						Show team names-->
                    <!--					</label>-->

                    <!--					<label class="checkbox" style="margin-left: 4em;">-->
                    <!--						<input type="checkbox" id="show_neutral_jousts_checkbox"/>-->
                    <!--						Show neutral joust bannners-->
                    <!--					</label>-->
                    <!--					<label class="checkbox" style="margin-left: 4em;">-->
                    <!--						<input type="checkbox" id="show_defensive_jousts_checkbox"/>-->
                    <!--						Show defensive joust bannners-->
                    <!--					</label>-->
                </div>
            </div>


            <br>

            <div class="button is-dark echobutton small {dirty ? '' : 'hide'}" id="update_manual_values"
                 style="margin:auto; display: block; width: 15em;" on:click={manualClickHandler}>
                Update
            </div>


        </div>
    </div>
</div>


<script lang="ts">
    import {SparkWebsocket} from "$lib/js/spark_websocket.js";
    import Header from "$lib/components/Header.svelte";
    import 'bulma';

    let teamLogosDict = {}

    import {onDestroy, onMount} from 'svelte';
    import {httpPostAsync, httpGetAsync} from '$lib/js/util.js';
    import VisibilityConfigCheckbox from "$lib/components/VisibilityConfigCheckbox.svelte";


    let sw = new SparkWebsocket();
    let config = {
        "visibility": {
            "minimap": false,
            "compact_minimap": false,
            "player_rosters": false,
            "main_banner": false,
            "neutral_jousts": false,
            "defensive_jousts": false,
            "event_log": false,
            "playspace": false,
            "player_speed": false,
            "disc_speed": false,
            "show_team_logos": false,
            "show_team_names": false
        },
        "caster_prefs": {},
        "round_scores": {
            "manual_round_scores": false,
            "round_count": 0,
            "round_scores_orange": [0],
            "round_scores_blue": [0]
        },
        "team_names_source": 1,
        "teams": [
            {
                "vrml_team_name": "",
                "vrml_team_logo": "",
                "team_name": "",
                "team_logo": ""
            },
            {
                "vrml_team_name": "",
                "vrml_team_logo": "",
                "team_name": "",
                "team_logo": ""
            }]
    };
    let isLoading = true;
    let dirty = false;
    sw.subscribe("overlay_config", data => {
        config = data;
        isLoading = false;
    });
    onMount(() => {

        console.log("HERE2");
    })
    onDestroy(() => sw.close());

    // set up listeners
    function swapSides() {
        isLoading = true;
        httpPostAsync('http://localhost:6724/api/set_team_details/orange', {
            "team_logo": config['teams'][0]['team_logo'],
            "team_name": config['teams'][0]['team_name'],
        });

        httpPostAsync('http://localhost:6724/api/set_team_details/blue', {
            "team_logo": config['teams'][1]['team_logo'],
            "team_name": config['teams'][1]['team_name'],
        });
    }


    function setTeamNamesSource(checked: boolean) {
        httpPostAsync('http://localhost:6724/api/set_team_names_source/' + (checked ? "1" : "0"), "");
    }


    function manualClickHandler() {
        httpPostAsync('http://localhost:6724/api/set_team_details/blue', {
            "team_logo": config['teams'][0]['team_logo'],
            "team_name": config['teams'][0]['team_name'],
        });

        httpPostAsync('http://localhost:6724/api/set_team_details/orange', {
            "team_logo": config['teams'][1]['team_logo'],
            "team_name": config['teams'][1]['team_name'],
        });
        dirty = false;
    }

    function manualValueChanged() {
        dirty = true;
    }


    httpGetAsync('https://api.ignitevr.gg/vrml/get_team_list', autocompleteTeamInputs);

    function autocompleteTeamInputs(data: string) {
        let parsed = JSON.parse(data);
        let teams: string[] = [];

        parsed.forEach(t => {
            teams.push(t['teamName']);
            teamLogosDict[t['teamName']] = 'https://vrmasterleague.com' + t['teamLogo'];
        });

        Array.from(document.getElementsByClassName("team_input")).forEach(e => {
            autocomplete(e, teams, 0, teamAutocompleteFinished);
        });
    }

    function teamAutocompleteFinished(inputElement) {
        if (inputElement.classList.contains("team_name_input_orange")) {
            httpPostAsync('http://localhost:6724/api/set_team_details/orange', {
                "team_name": inputElement.value,
                "team_logo": teamLogosDict[inputElement.value],
            });
        } else if (inputElement.classList.contains("team_name_input_blue")) {
            httpPostAsync('http://localhost:6724/api/set_team_details/blue', {
                "team_name": inputElement.value,
                "team_logo": teamLogosDict[inputElement.value],
            });
        }
    }


</script>
