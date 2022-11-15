<script lang="ts">
    import {httpPostAsync} from '../js/util';

    export let key: string = "";
    export let checked: boolean = false;
    export let text: string = "";


    function visibilityChanged(key, value) {
        let body = {"configurableOverlaySettings": {}};
        body["configurableOverlaySettings"][key] = value;
        httpPostAsync('http://localhost:6724/api/settings/set', body);
    }
</script>

<label class="checkbox" style="margin-left: 3em;">
	<input type="checkbox" {checked} on:click={(e)=>{visibilityChanged(key, e.target.checked)}}/>
	{text}
</label>