// busMap.js: draws the live bus map with Leaflet (the global "L" object comes from leaflet.js).
// The C# page (LiveMap.razor) calls these functions through Blazor's JS interop,
// e.g. JS.InvokeVoidAsync("busMap.create", "bus-map").
// The data arrives as JSON, so the C# property names become camelCase here
// (RouteName -> routeName, PositionTime -> positionTime).

window.busMap = {
    map: null,          // the Leaflet map object, or null when no map is on screen
    markersById: {},    // vehicleId -> marker, so a refresh moves existing markers instead of redrawing all

    // Creates the map inside the element with this id, centred on Dublin city centre.
    create: function (elementId) {
        // A map from an earlier visit to the page must be removed before making a new one.
        this.destroy();

        // preferCanvas draws the ~1,600 circle markers on one canvas, which is much faster
        // than creating one HTML element per bus.
        this.map = L.map(elementId, { preferCanvas: true }).setView([53.3498, -6.2603], 12);

        // The map pictures ("tiles") come from OpenStreetMap. Their licence requires this attribution text.
        L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(this.map);
    },

    // Shows the given buses: moves the ones already on the map, adds new ones, removes ones that left the feed.
    // Moving instead of redrawing keeps an open pop-up open across refreshes.
    showVehicles: function (vehicles) {
        if (this.map === null) { return; }

        const stillPresent = {};   // ids seen in this refresh

        for (const vehicle of vehicles) {
            stillPresent[vehicle.vehicleId] = true;
            let marker = this.markersById[vehicle.vehicleId];

            if (marker) {
                marker.setLatLng([vehicle.latitude, vehicle.longitude]);
            } else {
                marker = L.circleMarker([vehicle.latitude, vehicle.longitude], {
                    radius: 6,
                    color: "#ffffff",       // white outline so buses stand out on the map
                    weight: 1,
                    fillColor: "#0b5ed7",   // Bootstrap's blue, to match the rest of the site
                    fillOpacity: 0.9
                });
                // The pop-up is built when it is opened, from the marker's latest data.
                marker.bindPopup(() => buildPopup(marker.vehicle));
                marker.addTo(this.map);
                this.markersById[vehicle.vehicleId] = marker;
            }

            marker.vehicle = vehicle;   // keep the latest data for the pop-up
        }

        // Remove buses that are no longer in the feed.
        for (const id of Object.keys(this.markersById)) {
            if (!stillPresent[id]) {
                this.markersById[id].remove();
                delete this.markersById[id];
            }
        }
    },

    // Removes the map and frees its memory. Called when the user leaves the page.
    destroy: function () {
        if (this.map !== null) {
            this.map.remove();
            this.map = null;
        }
        this.markersById = {};
    }
};

// Builds the pop-up content as page elements. textContent (not innerHTML) is used on purpose:
// it always treats the feed's text as plain text, so it can never inject HTML into the page.
function buildPopup(vehicle) {
    const box = document.createElement("div");

    const title = document.createElement("strong");
    title.textContent = "Route " + vehicle.routeName;
    box.appendChild(title);

    if (vehicle.routeLongName) {
        box.appendChild(document.createElement("br"));
        box.appendChild(document.createTextNode(vehicle.routeLongName));
    }

    box.appendChild(document.createElement("br"));
    const age = document.createElement("small");
    if (vehicle.positionTime > 0) {
        // Date.now() is in milliseconds; the feed's time is in seconds.
        const minutes = Math.max(0, Math.round((Date.now() / 1000 - vehicle.positionTime) / 60));
        age.textContent = "Position reported " + minutes + " minute(s) ago";
    } else {
        age.textContent = "Position time not reported";
    }
    box.appendChild(age);

    return box;
}
