-- latency.lua - LAG + auto-restart przy >500ms

local visible = true
local ov = mp.create_osd_overlay("ass-events")

local restart_threshold = 500 -- ms
local restarting = false

function draw_stats()
    if not visible then return end

    -- pobranie lagu z bufora
    local buffer = mp.get_property_number("demuxer-cache-duration") or 0
    local ms_delay = math.floor(buffer * 1000)

    -- format czasu
    local lag_text = ms_delay .. " ms"
    if ms_delay > 1000 then
        lag_text = string.format("%.1f s", ms_delay / 1000)
    end

    -- kolory
    local color = "{\\c&H00FF00&}" -- zielony
    if ms_delay > 100 then color = "{\\c&H00FFFF&}" end -- zolty
    if ms_delay > 500 then color = "{\\c&H0000FF&}" end -- czerwony

    -- styl OSD
    local style = "{\\an9}{\\bord1}{\\fs18}"
    ov.data = style .. color .. "LAG: " .. lag_text
    ov:update()

    -- auto-restart jak przy klawiszu R
    check_restart(ms_delay)
end

function check_restart(ms_delay)
    if ms_delay > restart_threshold and not restarting then
        restarting = true
        local path = mp.get_property("path")
        if path then
            mp.commandv("loadfile", path, "replace")
        end
    end

    -- odblokowanie po uspokojeniu lagu
    if ms_delay < 200 then
        restarting = false
    end
end

function toggle_stats()
    visible = not visible
    if visible then
        draw_stats()
    else
        ov:remove()
    end
end

mp.add_periodic_timer(0.1, draw_stats)
mp.add_key_binding("TAB", "toggle_stats", toggle_stats)
