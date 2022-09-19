<script>
	import * as THREE from 'three';
	import {onDestroy, onMount} from "svelte";
	import {SparkWebsocket} from "../js/spark_websocket.js";
	import {empty_frame} from "../js/empty_frame";
	import {MeshLine, MeshLineMaterial, MeshLineRaycast} from 'three.meshline';

	let frame = null;

	const scene = new THREE.Scene();
	let camera;
	const geometry = new THREE.BoxGeometry();
	const material = new THREE.MeshBasicMaterial({color: 0x00ff00});
	const cube = new THREE.Mesh(geometry, material);
	let renderer;
	let blueGoal;
	let orangeGoal;

	let discPositions = [];
	const lineMaterial = new MeshLineMaterial({lineWidth: .04});
	const discLine = new MeshLine();
	const discLineGeo = new THREE.BufferGeometry();


	function animate() {
		requestAnimationFrame(animate);

		let pos = [
			-frame['player']['vr_position'][2],
			frame['player']['vr_position'][1],
			frame['player']['vr_position'][0]
		];

		camera.position.x = pos[0];
		camera.position.y = pos[1];
		camera.position.z = pos[2];

		let lookPos = new THREE.Vector3(
			pos[0] - frame['player']['vr_forward'][2],
			pos[1] + frame['player']['vr_forward'][1],
			pos[2] + frame['player']['vr_forward'][0]
		);
		camera.lookAt(lookPos);


		discPositions.push(new THREE.Vector3(
			-frame['disc']['position'][2],
			frame['disc']['position'][1],
			frame['disc']['position'][0]
		));
		if (discPositions.length > 100){
			discPositions.shift();
		}
		discLineGeo.setFromPoints(discPositions);
		discLine.setGeometry(discLineGeo)


		renderer.render(scene, camera);
	}

	function drawLine() {
		const material = new MeshLineMaterial({
			lineWidth: .04
		});

		fetch("http://localhost:6724/disc_positions").then(resp => resp.json()).then(j => {

			const points = [];

			console.log(j);

			for (let i = 0; i < j.length; i++) {
				points.push(new THREE.Vector3(-j[i]['z'], j[i]['y'], j[i]['x']));
			}

			const geometry = new THREE.BufferGeometry().setFromPoints(points);
			const line = new MeshLine();
			line.setGeometry(geometry);
			const mesh = new THREE.Mesh(line, material);
			scene.add(mesh);
		});

	}

	function resize() {
		renderer.setSize(window.innerWidth, window.innerHeight)
		camera.aspect = window.innerWidth / window.innerHeight;
		camera.updateProjectionMatrix();
	}

	function drawGoalLogo() {
		let loader = new THREE.TextureLoader();
		loader.crossOrigin = "";
		let texture = loader.load(
			"https://ignitevr.gg/images/logos/primary-white.png"
		);
		let img = new THREE.MeshBasicMaterial({
			opacity: .5,
			side: THREE.DoubleSide,
			map: texture,
			transparent: true,
		});

		blueGoal = new THREE.Mesh(new THREE.PlaneGeometry(1.5, 1.5), img);
		// goalMesh.rotateX(0.785398);	// 45 deg
		blueGoal.rotateY(-0.785398 * 2); // 90 deg
		blueGoal.position.x = 36.05;
		blueGoal.position.y = 0;
		blueGoal.position.z = 0;
		scene.add(blueGoal);

		orangeGoal = new THREE.Mesh(new THREE.PlaneGeometry(1.5, 1.5), img);
		// goalMesh.rotateX(0.785398);	// 45 deg
		orangeGoal.rotateY(0.785398 * 2); // 90 deg
		orangeGoal.position.x = -36.05;
		orangeGoal.position.y = 0;
		orangeGoal.position.z = 0;
		scene.add(orangeGoal);
	}

	function createScene(el) {
		// ~75 for sideline cam
		// 85 for freecam
		camera = new THREE.PerspectiveCamera(85, window.innerWidth / window.innerHeight, 0.1, 200);
		renderer = new THREE.WebGLRenderer({antialias: true, canvas: el, alpha: true});
		// scene.add(cube);


		discPositions.push(new THREE.Vector3(0,0,0));
		discPositions.push(new THREE.Vector3(10,10,10));
		discLineGeo.setFromPoints(discPositions);
		discLine.setGeometry(discLineGeo)
		const lineMesh = new THREE.Mesh(discLine, lineMaterial);
		scene.add(lineMesh);

		drawGoalLogo();
		// drawLine();

		resize();
		animate();
	}

	let el;
	let sw = new SparkWebsocket();

	onMount(() => {
		let firstLoad = true;
		sw.subscribe("frame_30hz", data => {
			if (data == null) return;
			frame = data;
			if (firstLoad) {
				firstLoad = false;
				createScene(el);
			}
		});
	});

	onDestroy(() => sw.close());
</script>

<canvas bind:this={el}></canvas>