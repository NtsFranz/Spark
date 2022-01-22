function fetch_overlay(interval = 1000, callback = null) {

    // const team_logo_orange = document.getElementsByClassName("team_logo_orange");
    // const team_name_orange = document.getElementsByClassName("team_name_orange");
    // const team_name_blue = document.getElementsByClassName("team_name_blue");
    // const team_logo_blue = document.getElementsByClassName("team_logo_blue");

    // const team_logo_input_orange = document.getElementsByClassName("team_logo_input_orange");
    // const team_name_input_orange = document.getElementsByClassName("team_name_input_orange");
    // const team_name_input_blue = document.getElementsByClassName("team_name_input_blue");
    // const team_logo_input_blue = document.getElementsByClassName("team_logo_input_blue");

    let lastStats = null;

    function get_data() {
        const url = `http://127.0.0.1:6724/overlay_info`;
        httpGetAsync(url, process_response);
    }

    function process_response(resp) {
        if (resp == "" || resp.length < 10) return;
        let data = JSON.parse(resp);
        if (data == null) return;


        // if the team has changed
        if (lastStats == null ||
            lastStats["stats"]["teams"][0]["team_name"] != data["stats"]["teams"][0]["team_name"] ||
            lastStats["stats"]["teams"][0]["team_logo"] != data["stats"]["teams"][0]["team_logo"]) {
            writeSrc("team_logo_blue", data["stats"]["teams"][0]["team_logo"]);
            writeValue("team_logo_input_blue", data["stats"]["teams"][0]["team_logo"]);
            writeText("team_name_blue", data["stats"]["teams"][0]["team_name"]);
            writeValue("team_name_input_blue", data["stats"]["teams"][0]["team_name"]);
        }
        // if the team has changed
        if (lastStats == null ||
            lastStats["stats"]["teams"][1]["team_name"] != data["stats"]["teams"][1]["team_name"] ||
            lastStats["stats"]["teams"][1]["team_logo"] != data["stats"]["teams"][1]["team_logo"]) {
                writeSrc("team_logo_orange", data["stats"]["teams"][1]["team_logo"]);
                writeValue("team_logo_input_orange", data["stats"]["teams"][1]["team_logo"]);
                writeText("team_name_orange", data["stats"]["teams"][1]["team_name"]);
                writeValue("team_name_input_orange", data["stats"]["teams"][1]["team_name"]);
        }

        if (callback != null) {
            callback(data, lastStats);
        }

        lastStats = data;
    }

    get_data();

    setInterval(get_data, interval);
}
