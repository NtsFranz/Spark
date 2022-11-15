export function httpGetAsync(theUrl, callback = null, failCallback = null) {
	fetch(theUrl)
		.then(ret => ret.text())
		.then(ret => {
			if (callback !== null) {
				callback(ret);
			}
		})
		.catch(() => {
			if (failCallback !== null) {
				failCallback();
			}
		});
}

export function httpPostAsync(theUrl, body, callback = null, failCallback = null) {
	fetch(theUrl, {
		method: "POST",
		headers: {'Content-Type': 'application/json'},
		body: JSON.stringify(body)
	}).then(res => {
		if (callback != null) {
			callback(res);
		}
	});
}

export function toMinutesString(seconds) {
	const mins = seconds / 60;
	let secs = Math.floor(seconds % 60);
	if (secs < 10) {
		secs = "0" + secs;
	}
	return Math.floor(mins) + ":" + secs;
}

export function write(className, data) {
	if (data === undefined || data == null || data.toString() === 'undefined') {
		data = "";
	}

	const elements = document.getElementsByClassName(className);
	Array.from(elements).forEach(e => {
		e.innerHTML = data;
		if (data === "") {
			e.style.opacity = "0";
		} else {
			e.style.opacity = 1;
			e.classList.remove('hide');
		}
	});
}

export function writeText(className, data) {
	if (data === undefined || data == null || data.toString() === 'undefined') {
		data = "";
	}

	const elements = document.getElementsByClassName(className);
	Array.from(elements).forEach(e => {
		e.innerText = data;
		if (data === "") {
			e.style.opacity = "0";
		} else {
			e.style.opacity = 1;
			e.classList.remove('hide');
		}
	});
}

export function writeHREF(className, data) {
	if (data === undefined || data === null || data.includes('undefined')) return;

	const elements = document.getElementsByClassName(className);
	Array.from(elements).forEach(e => {
		e.href = data;
		e.style.opacity = 1;
		e.classList.remove('hide');
	});
}

export function writeValue(className, data) {
	if (data === undefined || data === null || data.includes('undefined')) return;

	const elements = document.getElementsByClassName(className);
	Array.from(elements).forEach(e => {
		e.value = data;
		e.style.opacity = 1;
		e.classList.remove('hide');
	});
}

export function writeTextValue(className, data) {
	if (data === undefined || data === null || data.includes('undefined')) {
		data = "";
	}

	const elements = document.getElementsByClassName(className);
	Array.from(elements).forEach(e => {
		e.value = data;
		e.style.opacity = 1;
		e.classList.remove('hide');
	});
}

export function writeValueId(idName, data) {
	if (data === undefined || data === null || data.includes('undefined')) return;

	const e = document.getElementById(idName);
	e.value = data;
	e.style.opacity = "1";
	e.classList.remove('hide');
}

export function writeChecked(className, data) {
	if (data === undefined) return;

	const elements = document.getElementsByClassName(className);
	Array.from(elements).forEach(e => {
		e.checked = data;
		e.style.opacity = 1;
		e.classList.remove('hide');
	});
}

export function writeSrc(className, src_) {
	if (src_ === undefined || src_ === "" || src_ === null) {
		src_ = "";
	}

	const elements = document.getElementsByClassName(className);
	Array.from(elements).forEach(e => {
		e.src = src_;
		if (src_ === "") {
			e.style.opacity = "0";
			e.src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
		} else {
			e.style.opacity = 1;
			e.classList.remove('hide');
		}
	});
}

export function setImage(className, src_) {
	if (src_ === undefined || src_ === "" || src_ === null) {
		src_ = "";
	}

	const elements = document.getElementsByClassName(className);
	Array.from(elements).forEach(e => {
		e.src = src_;
		if (src_ === "") {
			e.style.opacity = "0";
		} else {
			e.style.opacity = 1;
			e.classList.remove('hide');
		}
	});
}

export function removeAllChildNodes(parent) {
	while (parent.firstChild) {
		parent.removeChild(parent.firstChild);
	}
}